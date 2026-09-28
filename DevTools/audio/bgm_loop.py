# -*- coding: utf-8 -*-
"""
BGM の繰り返しの区間を探して、本番のファイルと試聴用のファイルを作る（1〜10本目・結果画面で使った手順）。

先に decode_mp3.ps1 で波形を取り出しておく（<出力フォルダ>/<名前>.f32 と .json）。
下の順に使う。<k> は拡張子なしの波形（例：%TEMP%\\CrazyBowling_audio\\t07）。途中の結果は <k>_*.npy に残る。

  1. python bgm_loop.py stats  <k>                      音量・張りつき・前後の無音・左右の広がり・4秒ごとの大きさ・mp3 の詰め物
  2. python bgm_loop.py tempo  <k>                      テンポの候補（低い音の立ち上がりの自己相関）
  3. python bgm_loop.py grid   <k> <BPM の候補> <始め秒> <終わり秒>
                                                         テンポと拍の格子（位相）を細かく合わせる。揺れ（前・中・後）と、4拍ごと・3拍ごとの低い音の強さ
  4. python bgm_loop.py struct <k> <終わり秒>            段落の区切り（前後8拍の響きがはっきり変わる拍）。mod4・mod16 で小節の頭を決める
                                                         ★4つ打ちの曲は低い音だけでは小節の頭が分からない。区切り（フィル）が4拍の頭に来るかで決める
  5. python bgm_loop.py attack <k>                      打撃の頭が拍の格子の何 ms 後ろにあるか（切る位置は打撃の頭の 4ms 手前にする）
  6. python bgm_loop.py loops  <k> <始まりの刻み（拍）> <長さの刻み（拍）> <最短（秒）> <終わりの上限（秒）>
                                                         繰り返しの区間の候補。直前・直後 0.6秒の細かい響き＋前後8拍の響きの並びで比べる
                                                         （4拍子なら始まり 4拍刻み・長さ 16〜32拍刻み。変拍子なら 1拍刻み）
                                                         ★拍の番号は曲の最初の拍から数える（途中から数えると小節の頭でなくなる）
  7. python bgm_loop.py build  <k> <本番の wav> <試聴用のフォルダ> <試聴用の名前> <案の JSON>
       案の JSON の例：
         {"A":{"mode":"cut","s":25.1636,"e":107.9222},"B":{"mode":"cut","s":43.7843,"e":126.5429}}
         mode：cut（s〜e を切り出す。先頭 40ms に終わりの直後を等ゲインで重ねる）
               overlay（s から L 秒。はみ出した尾を頭に小さくしながら重ねる。作曲者のループ版の詰め物を取るとき）
               trim（s0 サンプル目から最後まで。頭の詰め物だけ取る）／ asis（そのまま。比べる用）
       ★重ね方は等ゲイン（sin²／cos²）。等パワー（sin／cos）は同じような音どうしで最大 +3dB 大きくなる
       ★区間の中に張りつき（0.999 以上）があれば、ピークを −1dBFS に収める分だけ下げる（A と B で同じ倍率）
       案A を本番、各案の _seam.wav（つなぎ目の前後 10秒）・_twice.wav（つなぎ目を2回聞ける）を試聴用に書く
  8. python bgm_loop.py loud   <wav>…                   左右の平均の音量と、耳の感じ方に近い重み付き（K 特性）の大きさ
       1〜10本目の目安：鳴るとき（×音の表の音量）に左右の平均で約 −20.2dB・重み付きで約 −17.8。2つの測り方の平均で音量を決める
"""
import json
import os
import pickle
import sys

import numpy as np

from audiotools import clip_count, db, k_loudness, load, read_wav, rms, write_wav

H = 0.005  # 包絡の刻み（秒）
EDGES = 40 * 2 ** (np.arange(0, 29) / 3)


# ---------------------------------------------------------------- 包絡・響き
def envelopes(x, sr):
    m = x.mean(1)
    hop, n_fft = int(sr * H), 2048
    f = np.fft.rfftfreq(n_fft, 1 / sr)
    w = np.hanning(n_fft)
    n = len(m) // hop
    mp = np.concatenate([m, np.zeros(n_fft)])
    fr = np.lib.stride_tricks.sliding_window_view(mp, n_fft)[::hop][:n] * w
    s = np.abs(np.fft.rfft(fr, axis=1))
    return s[:, f < 150].sum(1), s.sum(1)


def onset(e):
    d = np.diff(np.log(e + 1e-9), prepend=0)
    d[d < 0] = 0
    return d


def bandspec(x, sr, t0, t1, n_fft=4096):
    seg = x[max(0, int(t0 * sr)):int(t1 * sr)].mean(1)
    f = np.fft.rfftfreq(n_fft, 1 / sr)
    s = 0
    for i in range(0, max(1, len(seg) - n_fft + 1), n_fft // 2):
        v = seg[i:i + n_fft]
        if len(v) < n_fft:
            v = np.pad(v, (0, n_fft - len(v)))
        s = s + np.abs(np.fft.rfft(v * np.hanning(n_fft))) ** 2
    return 10 * np.log10(np.array([s[(f >= EDGES[i]) & (f < EDGES[i + 1])].sum() for i in range(28)]) + 1e-12)


def cor(a, b):
    a = a - a.mean()
    b = b - b.mean()
    return float((a * b).sum() / np.sqrt((a * a).sum() * (b * b).sum()))


def grid_of(k):
    bpm, ph = np.load(k + '_grid.npy')
    return float(bpm), float(ph)


# ---------------------------------------------------------------- 1. stats
def cmd_stats(k):
    x, sr = load(k)
    a = np.abs(x).max(1)
    nz = np.where(a > 10 ** (-60 / 20))[0]
    left, right = x[:, 0], x[:, -1]
    print(f'{k}：{len(x) / sr:.2f}秒・{sr}Hz・{x.shape[1]}ch｜平均 左 {db(rms(left)):.1f} 右 {db(rms(right)):.1f}｜'
          f'ピーク {db(a.max()):.2f}dBFS 張りつき {clip_count(x)}｜'
          f'相関 {np.corrcoef(left, right)[0, 1] if x.shape[1] > 1 else 1:.2f}｜'
          f'−60dB 未満の無音 先頭 {nz[0] / sr:.2f}秒・末尾 {(len(a) - nz[-1]) / sr:.2f}秒')
    print('  4秒ごとの大きさ', ' '.join(f'{db(rms(x[i * sr * 4:(i + 1) * sr * 4])):.0f}' for i in range(int(len(x) / sr / 4) + 1)))
    first0 = int(np.argmax(a > 1e-7))
    last0 = len(a) - 1 - int(np.argmax(a[::-1] > 1e-7))
    print(f'  詰め物：頭の 0 が {first0} サンプル（{first0 / sr * 1000:.1f}ms）・尾の 0 が {len(a) - 1 - last0} サンプル')


# ---------------------------------------------------------------- 2. tempo
def cmd_tempo(k):
    x, sr = load(k)
    low, full = envelopes(x, sr)
    np.save(k + '_low.npy', low)
    np.save(k + '_full.npy', full)
    o = onset(low) + 0.5 * onset(full)
    t0, t1 = int(5 / H), int(min(len(o) * H - 8, 120) / H)
    oo = o[t0:t1] - o[t0:t1].mean()
    ac = np.correlate(oo, oo, 'full')[len(oo) - 1:]
    best = []
    for bpm in np.arange(60, 200, 0.05):
        p = 60 / bpm
        best.append((sum(ac[int(round(j * p / H))] for j in range(1, 9)), bpm))
    best.sort(reverse=True)
    seen = []
    for _, b in best:
        if all(abs(b - c) > 2 for c in seen):
            seen.append(b)
        if len(seen) >= 5:
            break
    print(k, 'テンポの候補', [round(float(b), 2) for b in seen])


# ---------------------------------------------------------------- 3. grid
def cmd_grid(k, bpm0, a, b):
    bpm0, a, b = float(bpm0), float(a), float(b)
    low, full = np.load(k + '_low.npy'), np.load(k + '_full.npy')
    o = onset(low) + 0.5 * onset(full)
    ti = np.arange(len(o)) * H

    def score(p, ph, aa=a, bb=b):
        ts = np.arange(ph, bb, p)
        ts = ts[ts > aa]
        return np.interp(ts, ti, o).mean()

    best = (0, 0, 0)
    for bpm in np.arange(bpm0 - 0.6, bpm0 + 0.6, 0.005):
        p = 60 / bpm
        for ph in np.arange(0, p, 0.002):
            s = score(p, ph)
            if s > best[0]:
                best = (s, bpm, ph)
    _, bpm, ph = best
    p = 60 / bpm
    print(f'{k} BPM {bpm:.3f} 1拍 {p:.4f}秒 格子の始まり {ph:.4f}秒')
    n = (b - a) / 3
    for i in range(3):
        aa, bb = a + i * n, a + (i + 1) * n
        d = max(np.arange(-0.04, 0.041, 0.002), key=lambda dd: score(p, ph + dd, aa, bb))
        print(f'  {aa:.0f}-{bb:.0f}秒の揺れ {d * 1000:+.0f}ms')
    beats = np.arange(ph, b, p)
    beats = beats[beats > a]
    v = np.interp(beats, ti, onset(low))
    idx = np.round((beats - ph) / p).astype(int)
    for m in (3, 4):
        print(f'  {m}拍ごとの低い音の立ち上がり', [round(float(v[idx % m == j].mean() / v.mean()), 2) for j in range(m)])
    np.save(k + '_grid.npy', np.array([bpm, ph]))


# ---------------------------------------------------------------- 4. struct
def cmd_struct(k, end):
    x, sr = load(k)
    bpm, ph = grid_of(k)
    p = 60 / bpm
    nbt = int((float(end) - ph) / p)
    fb = np.array([bandspec(x, sr, ph + i * p, ph + (i + 1) * p) for i in range(nbt)])
    nov = sorted(((1 - cor(fb[i - 8:i].mean(0), fb[i:i + 8].mean(0)), i) for i in range(8, nbt - 8)), reverse=True)
    out = []
    for v, i in nov:
        if all(abs(i - j) > 6 for _, j in out):
            out.append((v, i))
        if len(out) >= 12:
            break
    print(f'{k}（拍の格子 {ph:.3f}秒から {p:.4f}秒ごと）')
    for v, i in sorted(out, key=lambda z: z[1]):
        print(f'  拍 {i:4d}（mod4={i % 4} mod16={i % 16} mod32={i % 32}）{ph + i * p:7.2f}秒 変わり目 {v:.3f}')


# ---------------------------------------------------------------- 5. attack
def cmd_attack(k):
    x, sr = load(k)
    m = x.mean(1)
    bpm, ph = grid_of(k)
    p = 60 / bpm
    offs = np.arange(-40, 41, 2)
    acc = np.zeros(len(offs))
    n = 0
    for t in np.arange(ph + 8 * p, min(len(x) / sr - 10, 150), p):
        acc += [db(rms(m[int((t + o / 1000) * sr):int((t + o / 1000 + 0.002) * sr)])) for o in offs]
        n += 1
    acc /= n
    base = np.median(acc[:8])
    rise = [o for o, v in zip(offs, acc) if v > base + 1.5]
    print(f'{k}：格子の前後の平均の大きさ', ' '.join(f'{o:+d}:{v:.0f}' for o, v in zip(offs, acc) if o % 4 == 0))
    print(f'  前より 1.5dB 上がり始める位置 {rise[0] if rise else "?"}ms（切る位置はここの 4ms 手前。見えなければ格子のまま）')


# ---------------------------------------------------------------- 6. loops
def cmd_loops(k, sstep, lstep, minlen, endmax, off=0):
    x, sr = load(k)
    sstep, lstep, minlen, endmax = int(sstep), int(lstep), float(minlen), float(endmax)
    bpm, ph = grid_of(k)
    p = 60 / bpm
    n_fft = 4096
    f = np.fft.rfftfreq(n_fft, 1 / sr)
    band = (f > 40) & (f < 8000)

    def fine(t0, dur=0.6):
        i0 = max(0, int(round(t0 * sr)))
        out = []
        for ch in range(x.shape[1]):
            seg = x[i0:i0 + int(dur * sr), ch]
            s = 0
            for j in range(0, len(seg) - n_fft + 1, n_fft // 4):
                s = s + np.abs(np.fft.rfft(seg[j:j + n_fft] * np.hanning(n_fft))) ** 2
            out.append(10 * np.log10(s[band] + 1e-10))
        return np.concatenate(out)

    nb = int((endmax + 2 - ph) / p)
    bt = [ph + (off + i) * p for i in range(nb)]
    bt = [t for t in bt if t + p * 9 < len(x) / sr]
    first = int(np.ceil((max(0.7, p * 8 + 0.7) - ph) / p))
    pre = [fine(t - 0.6) for t in bt]
    post = [fine(t) for t in bt]
    blk = [bandspec(x, sr, t, t + p) for t in bt]

    def lv(t):
        return db(rms(x[int((t - 1) * sr):int(t * sr)]))

    res = []
    for a in range(first + (-first) % sstep, len(bt), sstep):
        for b in range(a + lstep, len(bt), lstep):
            if bt[b] > endmax:
                break
            if bt[b] - bt[a] < minlen:
                continue
            c1, c2 = cor(pre[a], pre[b]), cor(post[a], post[b])
            ctx = float(np.mean([cor(blk[a + j], blk[b + j]) for j in range(-8, 8) if 0 <= a + j and b + j < len(blk)]))
            res.append((c1 + c2 + ctx, c1, c2, ctx, abs(lv(bt[a]) - lv(bt[b])), a, b, bt[a], bt[b]))
    res.sort(reverse=True)
    print(f'{k}：1拍 {p:.4f}秒・始まり {sstep}拍刻み・長さ {lstep}拍刻み・{minlen}秒以上・終わり {endmax}秒まで｜候補 {len(res)}')
    print('  始まり秒 終わり秒 長さ(秒) 始まりの拍 拍数 直前 直後 前後8拍 直前1秒の音量差')
    for _, c1, c2, ctx, dl, a, b, ta, tb in res[:10]:
        print(f'  {ta:7.3f} {tb:7.3f} {tb - ta:7.2f} {a:4d} {b - a:4d} {c1:.3f} {c2:.3f} {ctx:.3f} {dl:.1f}dB')
    pickle.dump(res[:50], open(k + '_loops.pkl', 'wb'))


# ---------------------------------------------------------------- 7. build
def fine_len(x, sr, i0, length):
    """つなぎ目の前後3秒の波形の相関がいちばん高くなる長さのずれ（±10ms）。打ち込みの曲なら 0 のはず。"""
    m = x.mean(1)
    out = []
    for side, a in (('直後', i0), ('直前', i0 - 3 * sr)):
        ref = m[a:a + 3 * sr]
        best = None
        for d in range(-sr // 100, sr // 100 + 1):
            seg = m[a + length + d:a + length + d + len(ref)]
            if len(seg) < len(ref):
                continue
            c = np.dot(ref, seg) / np.sqrt(np.dot(ref, ref) * np.dot(seg, seg))
            if best is None or c > best[0]:
                best = (c, d)
        out.append((side, round(best[1] / sr * 1000, 2), round(float(best[0]), 3)))
    return out


def make(x, sr, c):
    mode = c['mode']
    if mode == 'cut':
        i0 = int(round(c['s'] * sr))
        length = int(round((c['e'] - c['s']) * sr))
        xf = int(0.040 * sr)
        y = x[i0:i0 + length].copy()
        w = (np.sin(np.linspace(0, np.pi / 2, xf)) ** 2)[:, None]
        y[:xf] = y[:xf] * w + x[i0 + length:i0 + length + xf] * (1 - w)
        c['長さのずれ'] = fine_len(x, sr, i0, length)
        return y
    if mode == 'overlay':
        i0 = int(round(c['s'] * sr))
        y = x[i0:i0 + int(round(c['L'] * sr))].copy()
        tail = x[i0 + len(y):]
        n = min(len(tail), len(y))
        y[:n] += tail[:n] * np.cos(np.linspace(0, np.pi / 2, n))[:, None]
        return y
    if mode == 'trim':
        return x[int(c['s0']):].copy()
    return x.copy()


def cmd_build(k, dst, pdir, pp, cands_json):
    x, sr = load(k)
    cands = json.loads(cands_json)
    ys = {name: make(x, sr, c) for name, c in cands.items()}
    clip = max(clip_count(y) for y in ys.values())
    g = 10 ** (-1 / 20) / max(np.abs(y).max() for y in ys.values()) if clip else 1.0
    print(f'{k}｜区間の中の張りつき（いちばん多い案）{clip}｜音量の倍率 {g:.4f}（{db(g):+.2f}dB）')
    os.makedirs(pdir, exist_ok=True)
    for name, y in ys.items():
        y = y * g
        n = len(y)
        yy = np.concatenate([y, y, y])
        seam = float(np.abs(np.diff(yy, axis=0)).max(1)[n - 1])
        typ = float(np.percentile(np.abs(np.diff(y, axis=0)).max(1), 99))
        print(f'  案{name} {cands[name]}｜{n}サンプル {n / sr:.3f}秒｜つなぎ目の段差 {seam:.4f}（ふつう 上位1% {typ:.4f}）｜'
              f'平均 左 {db(rms(y[:, 0])):.2f} 右 {db(rms(y[:, -1])):.2f}｜ピーク {db(np.abs(y).max()):.2f}dBFS 張りつき {clip_count(y)}')
        t10 = 10 * sr
        write_wav(os.path.join(pdir, f'{pp}_loop{name}_seam.wav'), yy[n - t10:n + t10], sr)
        write_wav(os.path.join(pdir, f'{pp}_loop{name}_twice.wav'), yy[n - t10:2 * n + t10], sr)
        if name == 'A':
            write_wav(dst, y, sr)
            print(f'  本番 → {dst}')


# ---------------------------------------------------------------- 8. loud
def cmd_loud(*paths):
    for p in paths:
        y, sr = read_wav(p)
        print(f'{os.path.basename(p)}｜左右の平均 {db(rms(y)):.2f}dB｜重み付き（K 特性）{k_loudness(y, sr):.2f}')


if __name__ == '__main__':
    cmds = {'stats': cmd_stats, 'tempo': cmd_tempo, 'grid': cmd_grid, 'struct': cmd_struct, 'attack': cmd_attack,
            'loops': cmd_loops, 'build': cmd_build, 'loud': cmd_loud}
    if len(sys.argv) < 2 or sys.argv[1] not in cmds:
        print(__doc__)
        sys.exit(1)
    cmds[sys.argv[1]](*sys.argv[2:])

# -*- coding: utf-8 -*-
"""
加工A：声を 1940〜50年代の AM ラジオから流れてくる音にする（タイトルのナレーション・DJ のラジオ番組で使う）。

使い方：
  python voice_radio_a.py <decode_mp3.ps1 の出力（拡張子なし）> <書き出す wav> [--seed 101] [--match <見本の wav>]
    --match を付けると、「声が出ている所の大きさ」をその wav にそろえる
    （ふつうは Assets/Audio/Voice/vo_title_narration.wav。付けなければ元の声の平均の大きさのまま）
  例：python voice_radio_a.py %TEMP%\\CrazyBowling_audio\\c01 C:\\dev\\CrazyBowling\\Assets\\Audio\\Voice\\vo_dj_corner01.wav --seed 201 --match C:\\dev\\CrazyBowling\\Assets\\Audio\\Voice\\vo_title_narration.wav

順番（音源一覧.md の「タイトルのナレーション」の節の説明どおり。2026-09-28 に作り直した）：
  1. 前後の無音を削る（−50dB を超える所の 50ms 手前から、最後に超えた所の 50ms 後まで。頭 10ms・最後 80ms のフェード）
  2. 500〜2,500Hz に絞る（3次のバターワース）
  3. 小さなスピーカーの箱鳴り：1,250Hz を中心に +4dB（幅 0.7オクターブ）
  4. 真空管のひずみ：ピークを DRIVE_IN（1.35）にそろえてから tanh(2.2x + 0.15x²)（2乗の項で偶数次の響きを少し）
  5. 450〜3,200Hz に戻す（2次）
  6. 雑音（白色雑音の 300Hz より下を絞り、8kHz あたりから上を少し落とす。声より 18dB 小さい）と
     レコード針のぱちぱち（1秒に約 5.5回。山は中央 −25.7dB、1割は −21〜−15.5dB と大きめ。300Hz より下は絞る）
  7. 平均の大きさを元の声にそろえる（--match があれば、声が出ている所の大きさを見本にそろえる）

★DRIVE_IN は記録に無かった値。タイトルの元の声 30秒で「ピークと平均の差」が本番（17.0dB）と同じになる値として決めた。
  作り直しを本番の vo_title_narration.wav と比べた結果：3分の1オクターブの響きの似方 0.995（200〜6,400Hz は ±1dB 以内）・
  聞き取りの目安（加工なしとの 300〜3,000Hz の強弱の相関）0.936（本番 0.930）
"""
import argparse

import numpy as np

from audiotools import (band_share, biquad, bp2, bp3, clip_count, db, frames, load, onepole, rbj, read_wav, rms,
                        voiced_level, write_wav)

DRIVE_IN = 1.35
NOISE_DB = -18.0
CRACKLE_PER_SEC = 5.5


def trim(x, sr, th_db=-50.0, pre=0.05, post=0.05):
    a = np.abs(x)
    idx = np.where(a > 10 ** (th_db / 20))[0]
    s = max(0, idx[0] - int(pre * sr))
    e = min(len(x), idx[-1] + int(post * sr))
    y = x[s:e].copy()
    fi, fo = int(0.010 * sr), int(0.080 * sr)
    y[:fi] *= np.linspace(0, 1, fi)
    y[-fo:] *= np.linspace(1, 0, fo)
    return y, s / sr, e / sr


def noise_and_crackle(n, sr, voice_rms, seed):
    rng = np.random.default_rng(seed)
    w = rng.standard_normal(n)
    w = biquad(w, *rbj(sr, 'hp', 300, q=0.7071))
    w = biquad(w, *rbj(sr, 'hp', 300, q=0.7071))
    w = onepole(w, sr, 8000, 'lp')
    w *= voice_rms * 10 ** (NOISE_DB / 20) / rms(w)
    c = np.zeros(n)
    t = 0.0
    while True:
        t += rng.exponential(1 / CRACKLE_PER_SEC)
        i = int(t * sr)
        if i >= n - 200:
            break
        big = rng.random() < 0.1
        level = rng.uniform(-21, -15.5) if big else rng.normal(-25.7, 2.5)
        amp = 10 ** (level / 20) * voice_rms / 10 ** (-21.2 / 20)
        length = int(sr * rng.uniform(0.0005, 0.0015))
        c[i:i + length] += amp * np.sign(rng.standard_normal()) * np.exp(-np.arange(length) / (length / 3))
    c = biquad(c, *rbj(sr, 'hp', 300, q=0.7071))
    return w + c


def process_a(x, sr, seed):
    """加工A。x はモノラル。戻り値は (加工した音, 使った区間の始まり秒, 終わり秒)。"""
    y, s, e = trim(x, sr)
    ref = rms(y)
    z = bp3(y, sr, 500, 2500)
    z = biquad(z, *rbj(sr, 'peak', 1250, gain_db=4, bw=0.7))
    z = z / np.abs(z).max() * DRIVE_IN
    z = np.tanh(2.2 * z + 0.15 * z * z)
    z = z - np.mean(z)
    z = bp2(z, sr, 450, 3200)
    z *= ref / rms(z)
    z = z + noise_and_crackle(len(z), sr, rms(z), seed)
    z *= ref / rms(z)
    return z, s, e


def intelligibility(original, processed, sr):
    """聞き取りの目安：300〜3,000Hz の 10ms ごとの強弱が、加工なしとどれだけ同じか（相関）。"""
    def env(v):
        f = frames(bp2(v, sr, 300, 3000), sr, 10)
        return np.sqrt((f ** 2).mean(1))
    a, b = env(original), env(processed)
    n = min(len(a), len(b))
    return float(np.corrcoef(a[:n], b[:n])[0, 1])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('src')
    ap.add_argument('dst')
    ap.add_argument('--seed', type=int, default=1)
    ap.add_argument('--match')
    a = ap.parse_args()
    x, sr = load(a.src)
    x = x.mean(1)
    z, s, e = process_a(x, sr, a.seed)
    if a.match:
        ref, rsr = read_wav(a.match)
        target, _ = voiced_level(ref.mean(1), rsr)
        # 「声が出ている所」は決まった線（−33dB）で選ぶので、音量を変えると選ばれる所も変わる。ずれが 0.05dB 未満になるまで繰り返す
        # （元の音量が大きく違う声で、1回では 2dB 以上ずれた。ID 3本は1回でほぼ合っていた）
        for _ in range(20):
            now, _ = voiced_level(z, sr)
            if abs(target - now) < 0.05:
                break
            z *= 10 ** ((target - now) / 20)
    write_wav(a.dst, z, sr)
    lv, share = voiced_level(z, sr)
    print(f'{a.dst}｜元 {len(x) / sr:.3f}秒 → 元の {s:.3f}〜{e:.3f}秒を使い {len(z) / sr:.3f}秒｜'
          f'声が出ている所 {lv:.2f}dB（全体の {share:.0%}）・全体の平均 {db(rms(z)):.1f}dB・ピーク {db(np.abs(z).max()):.2f}dBFS・'
          f'張りつき {clip_count(z)}｜500〜2500Hz {band_share(z, sr, 500, 2500):.0%}｜'
          f'聞き取りの目安 {intelligibility(x[int(s * sr):int(e * sr)], z, sr):.3f}｜元の声 平均 {db(rms(x)):.1f}dB・ピーク {db(np.abs(x).max()):.2f}dBFS')


if __name__ == '__main__':
    main()

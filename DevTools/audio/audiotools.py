# -*- coding: utf-8 -*-
"""
音の取り込みで使う共通の計算（numpy だけで動く。scipy は入っていない）。

- 波形は decode_mp3.ps1 が書き出した <名前>.f32 と <名前>.json から読む（load）
- wav の読み書き（read_wav / write_wav。16bit）
- 大きさの測り方（db / rms / voiced_level / k_loudness）
- フィルター（biquad / onepole / rbj / bp2 / bp3。1〜4本目・加工A と同じ作り）
- mp3 の長さ（mp3_length。フレームの見出しを数える。ダウンロードしたファイルの長さをユーザーに見せるとき）
"""
import json
import math
import os
import wave

import numpy as np


# ---------------------------------------------------------------- 読み書き
def load(path_without_ext):
    """decode_mp3.ps1 の出力（.f32 と .json）を読む。戻り値は (波形[サンプル, チャンネル], 周波数)。"""
    meta = json.load(open(path_without_ext + '.json', encoding='utf-8-sig'))
    x = np.fromfile(path_without_ext + '.f32', dtype=np.float32).astype(np.float64)
    return x.reshape(-1, meta['ch']), meta['sr']


def read_wav(path):
    """16bit の wav を読む。戻り値は (波形[サンプル, チャンネル], 周波数)。"""
    with wave.open(path) as w:
        a = np.frombuffer(w.readframes(w.getnframes()), dtype='<i2').astype(np.float64) / 32768
        return a.reshape(-1, w.getnchannels()), w.getframerate()


def write_wav(path, y, sr):
    """16bit の wav に書く。y は [サンプル] か [サンプル, チャンネル]。"""
    y = np.asarray(y)
    if y.ndim == 1:
        y = y[:, None]
    q = np.clip(np.round(y * 32767), -32768, 32767).astype('<i2')
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with wave.open(path, 'wb') as w:
        w.setnchannels(y.shape[1])
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(q.tobytes())


# ---------------------------------------------------------------- 大きさ
def db(v):
    return 20 * math.log10(max(float(v), 1e-12))


def rms(a):
    return float(np.sqrt(np.mean(np.asarray(a) ** 2)))


def frames(x, sr, ms):
    n = int(sr * ms / 1000)
    m = len(x) // n
    return x[:m * n].reshape(m, n)


def voiced_level(x, sr, th_db=-33.0):
    """声が出ている所（20ms ごとの大きさが th_db 以上）の平均の大きさ（dB）と、その割合。
    加工A の雑音（約 −39dB）は入らない。声の音量をそろえるときに使う。"""
    f = frames(np.asarray(x), sr, 20)
    r = np.sqrt((f ** 2).mean(1))
    a = r > 10 ** (th_db / 20)
    return db(np.sqrt((f[a] ** 2).mean())), float(a.mean())


def clip_count(y, th=0.999):
    """最大値に張りつくサンプルの数。"""
    return int((np.abs(y) >= th).sum())


def band_share(x, sr, lo, hi):
    """lo〜hi Hz に入っている力の割合。"""
    s = np.abs(np.fft.rfft(np.asarray(x).reshape(len(x), -1).mean(1))) ** 2
    f = np.fft.rfftfreq(len(x), 1 / sr)
    return float(s[(f >= lo) & (f < hi)].sum() / s[f >= 20].sum())


# ---------------------------------------------------------------- フィルター（1サンプルずつ。短い音なら十分速い）
def biquad(x, b, a):
    y = np.empty_like(x)
    x1 = x2 = y1 = y2 = 0.0
    b0, b1, b2 = b
    a1, a2 = a
    for i in range(len(x)):
        v = b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, x[i]
        y2, y1 = y1, v
        y[i] = v
    return y


def onepole(x, sr, fc, kind):
    """1次の低域・高域通過（双一次変換）。"""
    k = math.tan(math.pi * fc / sr)
    if kind == 'lp':
        b = (k / (1 + k), k / (1 + k), 0.0)
    else:
        b = (1 / (1 + k), -1 / (1 + k), 0.0)
    return biquad(x, b, ((k - 1) / (k + 1), 0.0))


def rbj(sr, kind, fc, q=None, gain_db=0.0, bw=None):
    """RBJ の式の2次フィルター（'lp'・'hp'・'peak'）。bw はオクターブの幅。"""
    w = 2 * math.pi * fc / sr
    cw, sw = math.cos(w), math.sin(w)
    alpha = sw * math.sinh(math.log(2) / 2 * bw * w / sw) if bw is not None else sw / (2 * q)
    amp = 10 ** (gain_db / 40)
    if kind == 'lp':
        b, a = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2], [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == 'hp':
        b, a = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2], [1 + alpha, -2 * cw, 1 - alpha]
    else:
        b, a = [1 + alpha * amp, -2 * cw, 1 - alpha * amp], [1 + alpha / amp, -2 * cw, 1 - alpha / amp]
    return (b[0] / a[0], b[1] / a[0], b[2] / a[0]), (a[1] / a[0], a[2] / a[0])


def bp3(x, sr, lo, hi):
    """3次のバターワースの帯域通過（1次＋2次 Q=1 を高い側・低い側に）。"""
    x = onepole(x, sr, lo, 'hp')
    x = biquad(x, *rbj(sr, 'hp', lo, q=1.0))
    x = onepole(x, sr, hi, 'lp')
    return biquad(x, *rbj(sr, 'lp', hi, q=1.0))


def bp2(x, sr, lo, hi):
    """2次のバターワースの帯域通過。"""
    x = biquad(x, *rbj(sr, 'hp', lo, q=0.7071))
    return biquad(x, *rbj(sr, 'lp', hi, q=0.7071))


# ---------------------------------------------------------------- 耳の感じ方に近い大きさ（K 特性。BGM の音量をそろえるとき）
def k_loudness(x, sr, starts=(0.1, 0.45, 0.8), seconds=10):
    """ITU-R BS.1770 の K 特性で重み付けした大きさ（LUFS 相当）。速さのため、曲の3か所から seconds 秒ずつ測る。"""
    x = np.asarray(x).reshape(len(x), -1)
    f0, g, q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    k = math.tan(math.pi * f0 / sr)
    vh = 10 ** (g / 20)
    vb = vh ** 0.4996667741545416
    a0 = 1 + k / q + k * k
    b1 = ((vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0)
    a1 = (2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0)
    f0, q = 38.13547087602444, 0.5003270373238773
    k = math.tan(math.pi * f0 / sr)
    d = 1 + k / q + k * k
    b2 = (1.0, -2.0, 1.0)
    a2 = (2 * (k * k - 1) / d, (1 - k / q + k * k) / d)
    zs = []
    for s in starts:
        seg = x[int(len(x) * s):int(len(x) * s) + sr * seconds]
        tot = 0.0
        for c in range(seg.shape[1]):
            y = biquad(biquad(seg[:, c], b1, a1), b2, a2)
            tot += float(np.mean(y ** 2))
        zs.append(tot)
    return -0.691 + 10 * math.log10(np.mean(zs))


# ---------------------------------------------------------------- mp3 の長さ
_BR = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0]
_SRS = [44100, 48000, 32000, 0]


def mp3_length(path):
    """mp3（MPEG-1 Layer III）のフレームを数えて長さ（秒）を出す。戻り値は (秒, 周波数, ビットレートの一覧)。"""
    b = open(path, 'rb').read()
    i = 0
    if b[:3] == b'ID3':
        i = 10 + ((b[6] & 0x7f) << 21 | (b[7] & 0x7f) << 14 | (b[8] & 0x7f) << 7 | (b[9] & 0x7f))
    n, sr, kb = 0, 0, set()
    while i + 4 <= len(b):
        if b[i] == 0xFF and (b[i + 1] & 0xE0) == 0xE0:
            ver, lay = (b[i + 1] >> 3) & 3, (b[i + 1] >> 1) & 3
            bri, sri, pad = b[i + 2] >> 4, (b[i + 2] >> 2) & 3, (b[i + 2] >> 1) & 1
            if ver == 3 and lay == 1 and 0 < bri < 15 and sri < 3:
                sr = _SRS[sri]
                kb.add(_BR[bri])
                n += 1
                i += 144000 * _BR[bri] // sr + pad
                continue
        i += 1
    return (n * 1152 / sr if sr else 0.0), sr, sorted(kb)


if __name__ == '__main__':
    # 使い方：python audiotools.py <mp3 のファイル>… で長さを出す
    import sys
    for p in sys.argv[1:]:
        sec, sr, kb = mp3_length(p)
        print(f'{os.path.basename(p)}｜{sec:.2f}秒｜{sr}Hz｜{kb}kbps')

# WebGL のビルドを、このパソコンのブラウザで確かめるための小さなサーバー
#   python DevTools/serve_webgl.py            （Builds/WebGL を http://localhost:8765/ で出す）
#   python DevTools/serve_webgl.py <フォルダ> <番号>
#   python DevTools/serve_webgl.py --slow 400          （1秒あたり 400KB に絞る。スマホの回線のまねで、読み込み画面を確かめる）
#   python DevTools/serve_webgl.py --slow 400 --no-length （全体の大きさ Content-Length を付けない。進み具合が届かない配信のまね）
# gzip のファイル（.gz）には Content-Encoding: gzip を付けて渡す（PLiCy と同じく、ブラウザが自分で展開する）。
# ★記録（PlayerPrefs）はブラウザの保存領域に「アドレスごと」に残るので、確かめるときは番号を変えないこと。
# 止めるときは Ctrl+C。このパソコンの中（127.0.0.1）からしか見えない。
import argparse
import http.server
import os
import time

parser = argparse.ArgumentParser(description="WebGL のビルドを確かめるサーバー")
parser.add_argument("root", nargs="?", default=os.path.join(os.path.dirname(__file__), "..", "Builds", "WebGL"))
parser.add_argument("port", nargs="?", type=int, default=8765)
parser.add_argument("--slow", type=float, default=0, help="1秒あたりに送る KB（0 なら絞らない）")
parser.add_argument("--no-length", action="store_true", help="Content-Length を付けない")
args = parser.parse_args()

ROOT = args.root
PORT = args.port

# .gz を外した名前の拡張子から、中身の種類を決める
TYPES = {
    ".js": "application/javascript",
    ".wasm": "application/wasm",
    ".data": "application/octet-stream",
    ".symbols.json": "application/octet-stream",
}


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **kwargs):
        super().__init__(*a, directory=ROOT, **kwargs)

    def guess_type(self, path):
        if path.endswith(".gz"):
            inner = path[:-3]
            for ext, ctype in TYPES.items():
                if inner.endswith(ext):
                    return ctype
            return "application/octet-stream"
        return super().guess_type(path)

    def send_header(self, keyword, value):
        # 大きさを付けない配信のまね（HTTP/1.0 なので、送り終わったら接続を閉じて終わりを伝える）
        if args.no_length and keyword.lower() == "content-length" and "/Build/" in self.path:
            return
        super().send_header(keyword, value)

    def end_headers(self):
        if self.path.split("?")[0].endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        # 確かめのたびに作り直したビルドを確実に読むよう、キャッシュさせない
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def copyfile(self, source, outputfile):
        if args.slow <= 0 or "/Build/" not in self.path:
            return super().copyfile(source, outputfile)
        # 1秒あたり args.slow KB に絞って送る
        chunk = 16 * 1024
        per_chunk = chunk / (args.slow * 1024)
        while True:
            buf = source.read(chunk)
            if not buf:
                break
            outputfile.write(buf)
            time.sleep(per_chunk)


if __name__ == "__main__":
    print(f"出しているフォルダ：{os.path.abspath(ROOT)}")
    if args.slow > 0:
        print(f"1秒あたり {args.slow:.0f}KB に絞る")
    if args.no_length:
        print("全体の大きさ（Content-Length）を付けない")
    print(f"ブラウザで開く：http://localhost:{PORT}/")
    http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()

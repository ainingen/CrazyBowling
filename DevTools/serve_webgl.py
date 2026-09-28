# WebGL のビルドを、このパソコンのブラウザで確かめるための小さなサーバー
#   python DevTools/serve_webgl.py            （Builds/WebGL を http://localhost:8765/ で出す）
#   python DevTools/serve_webgl.py <フォルダ> <番号>
# gzip のファイル（.gz）には Content-Encoding: gzip を付けて渡す（PLiCy と同じく、ブラウザが自分で展開する）。
# ★記録（PlayerPrefs）はブラウザの保存領域に「アドレスごと」に残るので、確かめるときは番号を変えないこと。
# 止めるときは Ctrl+C。このパソコンの中（127.0.0.1）からしか見えない。
import http.server
import os
import sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "Builds", "WebGL")
PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 8765

# .gz を外した名前の拡張子から、中身の種類を決める
TYPES = {
    ".js": "application/javascript",
    ".wasm": "application/wasm",
    ".data": "application/octet-stream",
    ".symbols.json": "application/octet-stream",
}


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=ROOT, **kwargs)

    def guess_type(self, path):
        if path.endswith(".gz"):
            inner = path[:-3]
            for ext, ctype in TYPES.items():
                if inner.endswith(ext):
                    return ctype
            return "application/octet-stream"
        return super().guess_type(path)

    def end_headers(self):
        if self.path.split("?")[0].endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        # 確かめのたびに作り直したビルドを確実に読むよう、キャッシュさせない
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


if __name__ == "__main__":
    print(f"出しているフォルダ：{os.path.abspath(ROOT)}")
    print(f"ブラウザで開く：http://localhost:{PORT}/")
    http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()

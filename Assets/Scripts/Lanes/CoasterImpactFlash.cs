using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 10本目：ボールがピンに当たった瞬間の「衝突実験の閃光」。見た目だけ。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// ★判定と物理には一切関わらない。
    ///   ピンの速度を読むだけで、ピンにもボールにも書き込まない。当たり判定も持たない。
    ///   連鎖爆発（PinExplosion）の仕組みにも手を入れない。
    ///
    /// 「当たった瞬間」は、投げてから最初にピンが動き出した瞬間とみなす。
    /// 投げるたびに Arm() で1回ぶんの閃光を用意し、光ったら次に投げるまで光らない。
    /// </summary>
    public class CoasterImpactFlash : MonoBehaviour
    {
        [Header("きっかけ")]
        [Tooltip("ピンがこの速さ（m/s）を超えたら、当たったとみなす。")]
        [SerializeField] private float triggerSpeed = 0.6f;

        [Header("見た目")]
        [Tooltip("光源。瞬間だけ強く光らせる。")]
        [SerializeField] private Light flashLight;

        [Tooltip("光源の明るさの最大。")]
        [SerializeField] private float lightIntensity = 60f;

        [Tooltip("膨らむ光の玉（加算の半透明）。")]
        [SerializeField] private Renderer flashBall;

        [Tooltip("光の玉の最大の大きさ（m）。")]
        [SerializeField] private float ballMaxScale = 1.6f;

        [Tooltip("床に沿って広がる輪（加算の半透明）。")]
        [SerializeField] private Renderer shockRing;

        [Tooltip("輪の最大の大きさ（倍）。")]
        [SerializeField] private float ringMaxScale = 5f;

        [Tooltip("閃光の長さ（秒）。")]
        [SerializeField] private float duration = 0.45f;

        [Tooltip("光の玉と輪の色（明るさ込み）。")]
        [SerializeField, ColorUsage(false, true)] private Color flashColor = new Color(2.5f, 3.5f, 6f);

        [Tooltip("同時に弾けさせる背景のネオン。空でもよい。")]
        [SerializeField] private NeonFlow[] neons;

        [Tooltip("ネオンを弾けさせる長さ（秒）。")]
        [SerializeField] private float neonBurstSeconds = 1.2f;

        private Pins.PinSet _pinSet;
        private Ball.BallController _ball;
        private bool _armed;
        private float _elapsed = -1f;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>投げるたびに呼ぶ。次に最初にピンが動いたとき、1回だけ光る。</summary>
        public void Arm()
        {
            _armed = true;
        }

        private void OnEnable()
        {
            _pinSet = Object.FindFirstObjectByType<Pins.PinSet>();
            _ball = Object.FindFirstObjectByType<Ball.BallController>();
            _block = new MaterialPropertyBlock();
            _armed = false;
            _elapsed = -1f;
            Show(false);
        }

        private void OnDisable()
        {
            Show(false);
        }

        private void LateUpdate()
        {
            if (_armed && _ball != null && _ball.IsInPlay && TryFindMovingPin(out Vector3 position))
            {
                _armed = false;
                Fire(position);
            }

            if (_elapsed < 0f)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(duration, 0.01f));
            if (t >= 1f)
            {
                _elapsed = -1f;
                Show(false);
                return;
            }

            // 一瞬で強く光り、すぐ引く
            float fade = (1f - t) * (1f - t);
            if (flashLight != null)
            {
                flashLight.intensity = lightIntensity * fade;
            }

            if (flashBall != null)
            {
                flashBall.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, ballMaxScale, Mathf.Sqrt(t));
                SetColor(flashBall, flashColor * fade);
            }

            if (shockRing != null)
            {
                shockRing.transform.localScale = new Vector3(1f, 1f, 1f) * Mathf.Lerp(0.3f, ringMaxScale, t);
                SetColor(shockRing, flashColor * fade * 0.7f);
            }
        }

        /// <summary>止まっていたピンのうち、動き出したものを探す。速度を読むだけ。</summary>
        private bool TryFindMovingPin(out Vector3 position)
        {
            position = Vector3.zero;
            if (_pinSet == null)
            {
                return false;
            }

            float threshold = triggerSpeed * triggerSpeed;
            foreach (Pins.Pin pin in _pinSet.Pins)
            {
                if (pin == null || !pin.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var body = pin.GetComponent<Rigidbody>();
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                if (body.linearVelocity.sqrMagnitude > threshold)
                {
                    position = pin.transform.position;
                    return true;
                }
            }
            return false;
        }

        private void Fire(Vector3 position)
        {
            // 閃光の置き場所だけを動かす（この部品の子なので、共有物には触らない）
            transform.position = position;
            _elapsed = 0f;
            Show(true);

            if (neons != null)
            {
                foreach (NeonFlow neon in neons)
                {
                    if (neon != null)
                    {
                        neon.Burst(neonBurstSeconds);
                    }
                }
            }
        }

        private void Show(bool visible)
        {
            if (flashLight != null)
            {
                flashLight.enabled = visible;
                if (!visible)
                {
                    flashLight.intensity = 0f;
                }
            }

            if (flashBall != null)
            {
                flashBall.enabled = visible;
            }

            if (shockRing != null)
            {
                shockRing.enabled = visible;
            }
        }

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }
    }
}

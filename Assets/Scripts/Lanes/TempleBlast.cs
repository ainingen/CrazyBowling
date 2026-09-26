using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 9本目の神殿。ボールが入り口から中へ入ったら、神殿ごと吹き飛ばしてピンを全部倒す。
    ///
    /// ── 入ったかの判定 ───────────────────────────────
    ///
    /// 神殿の中心からの距離で見る（当たり判定は置かない）。
    /// 柱のすき間はボールより狭いので、中心の近くにボールがある＝入り口を通った、になる。
    /// 柱に阻まれた球の中心は、どう当たっても判定の半径より内側に来ない。
    /// トリガーの当たり判定にしないのは、ピンが常にその中にいて、見分ける手間が増えるだけだから。
    ///
    /// ── 爆発の分担 ────────────────────────────────
    ///
    /// ・結果：PinExplosion.ExplodeAt で、神殿の中心から強い爆発を1回起こしてピンを倒す。
    ///         強さはこの呼び出しだけの引数で渡すので、共有の設定は書き換えない。
    /// ・見た目：柱と屋根の見た目を破片として飛ばす。
    ///         **破片は Rigidbody も当たり判定も持たず、コードで放物線を描かせて地面の高さで弾ませる。**
    ///         ボールやピンとは絶対に当たらないので、判定が長引いたり壊れ方が変わったりしない。
    ///
    /// ── 後片付け ─────────────────────────────────
    ///
    /// 破片はレーンの子のまま残る。レーンを読み込み直せば、レーンごと必ず消える。
    /// 計測などで同じレーンのまま戻したいときは Restore() を呼ぶ。
    /// </summary>
    public class TempleBlast : MonoBehaviour
    {
        [Header("入ったかの判定")]
        [Tooltip("神殿の中心（円盤と一緒に回る Temple の体）。")]
        [SerializeField] private Transform center;

        [Tooltip("ボールの中心が、神殿の中心からこの距離（水平・m）より内側に来たら「入った」とみなす。" +
                 "柱に阻まれた球の中心が届く距離（約0.66）より小さく、ピンに触れる距離（約0.46）より大きくすること。")]
        [SerializeField] private float entryRadius = 0.50f;

        [Tooltip("ボールの中心が、神殿の中心よりこの高さ（m）を超えていたら数えない。跳ねて飛び越えた球を除くため。")]
        [SerializeField] private float entryMaxHeight = 0.5f;

        [Header("ピンを倒す爆発")]
        [Tooltip("吹き飛ばす力（速度の変化量・m/s）。入ったら10本全部倒れる強さにする。")]
        [SerializeField] private float blastForce = 9f;

        [Tooltip("効果が及ぶ範囲（m）。範囲の端でちょうど半分の強さになる。ピンは中心から0.35m以内にいる。")]
        [SerializeField] private float blastRadius = 1.0f;

        [Tooltip("上向き成分の割合。")]
        [Range(0f, 2f)]
        [SerializeField] private float blastUpwardRatio = 0.6f;

        [Header("破片（見た目だけ）")]
        [Tooltip("爆発したら当たり判定を切るもの（柱）。切らないとボールが中で止まる。")]
        [SerializeField] private Collider[] blockers;

        [Tooltip("破片として飛ばす見た目（柱・屋根・入り口の飾り）。当たり判定を付けないこと。")]
        [SerializeField] private Transform[] debris;

        [Tooltip("破片が外へ飛ぶ速さ（m/s）。")]
        [SerializeField] private float debrisOutSpeed = 5f;

        [Tooltip("破片が上へ飛ぶ速さ（m/s）。")]
        [SerializeField] private float debrisUpSpeed = 6f;

        [Tooltip("破片の回転の速さ（度/秒）の上限。")]
        [SerializeField] private float debrisSpin = 720f;

        [Tooltip("破片に掛ける重力（m/s²）。")]
        [SerializeField] private float debrisGravity = 9.81f;

        [Tooltip("破片が止まる地面の高さ（レーン基準・m）。ピン台の上面。")]
        [SerializeField] private float groundHeight = 0.05f;

        [Tooltip("レーンの幅の外（横の距離がこれより大きい所）へ飛んだ破片は、外の地面の高さで止める（m）。")]
        [SerializeField] private float laneHalfWidth = 0.8f;

        [Tooltip("レーンの外の地面の高さ（レーン基準・m）。宙に浮いて止まらないように背景の地面に合わせる。")]
        [SerializeField] private float outsideGroundHeight = -0.3f;

        [Tooltip("地面で跳ねるときに残す速さの割合。")]
        [Range(0f, 1f)]
        [SerializeField] private float debrisBounce = 0.3f;

        [Tooltip("地面に触れるたびに、横の速さと回転に掛ける率。")]
        [Range(0f, 1f)]
        [SerializeField] private float debrisGroundDamping = 0.6f;

        [Header("演出（見た目だけ。判定には関わらない）")]
        [Tooltip("閃光のライト。爆発の瞬間だけ強く光り、すぐ消える。")]
        [SerializeField] private Light flashLight;

        [Tooltip("閃光のいちばん強いときの明るさ。")]
        [SerializeField] private float flashIntensity = 60f;

        [Tooltip("閃光と光の球が消えるまでの秒数。")]
        [SerializeField] private float flashSeconds = 0.6f;

        [Tooltip("閃光の球（光るだけの見た目）。広がりながら消える。")]
        [SerializeField] private Renderer flashBall;

        [Tooltip("光の球が広がる大きさ（直径・m）。")]
        [SerializeField] private float flashBallSize = 3f;

        [Tooltip("光の球のいちばん明るいときの発光の倍率。強すぎると画面全体が一瞬白く飛ぶ" +
                 "（光に敏感な人への配慮で、画面全体を真っ白にしないこと）。")]
        [SerializeField] private float flashBallGlow = 12f;

        [Tooltip("衝撃波の輪（光るだけの見た目）。床に沿って、色を変えながら広がる。")]
        [SerializeField] private Renderer shockRing;

        [Tooltip("衝撃波の輪が広がる大きさ（直径・m）。")]
        [SerializeField] private float shockRingSize = 8f;

        [Tooltip("衝撃波の輪が消えるまでの秒数。")]
        [SerializeField] private float shockSeconds = 0.9f;

        [Tooltip("爆発の瞬間に噴き出す粒（光の粒・紙吹雪など）。")]
        [SerializeField] private ParticleSystem[] bursts;

        [Tooltip("爆発の瞬間に弾けさせるネオン（背景・祭壇など）。空なら何もしない。")]
        [SerializeField] private NeonFlow[] neons;

        [Tooltip("背景のネオンが弾けている秒数。")]
        [SerializeField] private float neonBurstSeconds = 2.5f;

        [Header("確認用")]
        [SerializeField] private bool logEvents = true;

        private bool _exploded;

        /// <summary>爆発してからの経過時間。演出を進めるのに使う。</summary>
        private float _effectTime;
        private MaterialPropertyBlock _effectBlock;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Vector3[] _velocity;
        private Vector3[] _spin;
        private bool[] _resting;

        /// <summary>元に戻すための、最初の親と姿勢。</summary>
        private Transform[] _originalParent;
        private Vector3[] _originalPosition;
        private Quaternion[] _originalRotation;
        private Vector3[] _originalScale;

        /// <summary>もう吹き飛んだか。</summary>
        public bool HasExploded => _exploded;

        /// <summary>破片の数。確認用。</summary>
        public int DebrisCount => debris == null ? 0 : debris.Length;

        private void Awake()
        {
            int count = debris == null ? 0 : debris.Length;
            _velocity = new Vector3[count];
            _spin = new Vector3[count];
            _resting = new bool[count];
            _originalParent = new Transform[count];
            _originalPosition = new Vector3[count];
            _originalRotation = new Quaternion[count];
            _originalScale = new Vector3[count];

            HideEffects();

            for (int i = 0; i < count; i++)
            {
                if (debris[i] == null)
                {
                    continue;
                }

                _originalParent[i] = debris[i].parent;
                _originalPosition[i] = debris[i].localPosition;
                _originalRotation[i] = debris[i].localRotation;
                _originalScale[i] = debris[i].localScale;
            }
        }

        /// <summary>ボールの中心がこの位置にあるとき、神殿に入ったとみなすか。</summary>
        public bool IsInside(Vector3 ballPosition)
        {
            if (center == null)
            {
                return false;
            }

            Vector3 offset = ballPosition - center.position;
            if (offset.y > entryMaxHeight)
            {
                return false;
            }

            offset.y = 0f;
            return offset.magnitude < entryRadius;
        }

        /// <summary>
        /// 神殿を吹き飛ばし、ピンを倒す。1回きり（Restore するまで）。
        /// ピンは降ろしてから呼ぶこと（kinematic のピンは爆発の対象外）。
        /// </summary>
        /// <returns>吹き飛ばしたピンの本数。</returns>
        public int Explode(Pins.PinExplosion explosion)
        {
            if (_exploded || center == null)
            {
                return 0;
            }

            _exploded = true;

            // 柱の当たり判定を切る。ボールは奥へ抜け、ピンも柱に引っかからない
            if (blockers != null)
            {
                foreach (Collider blocker in blockers)
                {
                    if (blocker != null)
                    {
                        blocker.enabled = false;
                    }
                }
            }

            // 見た目を破片として放つ。神殿の体から外し、レーンの子にしておく
            // （体は乗り物が動かすので、付いたままだと破片が引きずられる）
            Vector3 origin = center.position;
            for (int i = 0; i < debris.Length; i++)
            {
                Transform piece = debris[i];
                if (piece == null)
                {
                    continue;
                }

                piece.SetParent(transform, true);

                Vector3 outward = piece.position - origin;
                outward.y = 0f;
                if (outward.sqrMagnitude < 1e-6f)
                {
                    outward = Random.insideUnitSphere;
                    outward.y = 0f;
                }

                outward.Normalize();
                _velocity[i] = outward * debrisOutSpeed * Random.Range(0.7f, 1.3f)
                               + Vector3.up * debrisUpSpeed * Random.Range(0.6f, 1.2f);
                _spin[i] = Random.insideUnitSphere * debrisSpin;
                _resting[i] = false;
            }

            int blasted = explosion != null
                ? explosion.ExplodeAt(origin, blastForce, blastRadius, blastUpwardRatio)
                : 0;

            StartEffects(origin);

            if (logEvents)
            {
                Debug.Log($"9本目：ボールが神殿に入った。神殿ごと吹き飛ばし、{blasted}本を飛ばした", this);
            }

            return blasted;
        }

        /// <summary>
        /// 神殿を元に戻す。柱の当たり判定を戻し、破片を元の場所へ。
        /// レーンの読み込み直しでは要らない（レーンごと作り直される）。計測で同じレーンのまま戻すときに使う。
        /// </summary>
        public void Restore()
        {
            if (blockers != null)
            {
                foreach (Collider blocker in blockers)
                {
                    if (blocker != null)
                    {
                        blocker.enabled = true;
                    }
                }
            }

            if (debris != null && _originalParent != null)
            {
                for (int i = 0; i < debris.Length; i++)
                {
                    if (debris[i] == null)
                    {
                        continue;
                    }

                    debris[i].SetParent(_originalParent[i], false);
                    debris[i].localPosition = _originalPosition[i];
                    debris[i].localRotation = _originalRotation[i];
                    debris[i].localScale = _originalScale[i];
                    _velocity[i] = Vector3.zero;
                    _spin[i] = Vector3.zero;
                    _resting[i] = false;
                }
            }

            _exploded = false;
            HideEffects();
        }

        /// <summary>閃光・光の球・衝撃波・粒・背景のネオンを始める。</summary>
        private void StartEffects(Vector3 origin)
        {
            _effectTime = 0f;

            if (flashLight != null)
            {
                flashLight.transform.position = origin + Vector3.up * 0.6f;
                flashLight.enabled = true;
            }

            if (flashBall != null)
            {
                flashBall.transform.position = origin + Vector3.up * 0.3f;
                flashBall.gameObject.SetActive(true);
            }

            if (shockRing != null)
            {
                shockRing.transform.position = origin + Vector3.up * 0.02f;
                shockRing.gameObject.SetActive(true);
            }

            if (bursts != null)
            {
                foreach (ParticleSystem burst in bursts)
                {
                    if (burst == null)
                    {
                        continue;
                    }

                    burst.transform.position = origin + Vector3.up * 0.3f;
                    burst.Play(true);
                }
            }

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

            UpdateEffects();
        }

        /// <summary>演出を隠す。最初と、元に戻すときに呼ぶ。</summary>
        private void HideEffects()
        {
            if (flashLight != null)
            {
                flashLight.enabled = false;
            }

            if (flashBall != null)
            {
                flashBall.gameObject.SetActive(false);
            }

            if (shockRing != null)
            {
                shockRing.gameObject.SetActive(false);
            }

            if (bursts != null)
            {
                foreach (ParticleSystem burst in bursts)
                {
                    if (burst != null)
                    {
                        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
        }

        /// <summary>閃光・光の球・衝撃波の輪を、経過時間に合わせて進める。</summary>
        private void UpdateEffects()
        {
            if (_effectBlock == null)
            {
                _effectBlock = new MaterialPropertyBlock();
            }

            // 閃光：一瞬で最大になり、2乗で落ちる。色は白から虹色へ回る
            float flash = Mathf.Clamp01(1f - _effectTime / Mathf.Max(flashSeconds, 0.01f));
            Color flashColor = Color.Lerp(Color.HSVToRGB(Mathf.Repeat(_effectTime * 2.5f, 1f), 0.8f, 1f),
                                          Color.white, flash * flash);
            if (flashLight != null && flashLight.enabled)
            {
                flashLight.intensity = flashIntensity * flash * flash;
                flashLight.color = flashColor;
                if (flash <= 0f)
                {
                    flashLight.enabled = false;
                }
            }

            if (flashBall != null && flashBall.gameObject.activeSelf)
            {
                float grow = 1f - flash;
                flashBall.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, flashBallSize, Mathf.Sqrt(grow));
                SetGlow(flashBall, flashColor, flashBallGlow * flash * flash);
                if (flash <= 0f)
                {
                    flashBall.gameObject.SetActive(false);
                }
            }

            // 衝撃波：床に沿って広がり、色を回しながら薄れる
            float shock = Mathf.Clamp01(_effectTime / Mathf.Max(shockSeconds, 0.01f));
            if (shockRing != null && shockRing.gameObject.activeSelf)
            {
                float size = Mathf.Lerp(0.5f, shockRingSize, 1f - (1f - shock) * (1f - shock));
                shockRing.transform.localScale = new Vector3(size, 1f, size);
                Color ringColor = Color.HSVToRGB(Mathf.Repeat(_effectTime * 1.7f, 1f), 1f, 1f);
                SetGlow(shockRing, ringColor, 8f * (1f - shock));
                if (shock >= 1f)
                {
                    shockRing.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 光るだけの見た目の色を、マテリアルを書き換えずに差し替える。
        /// 光の球と衝撃波の輪は透明の加算描画なので、明るさは色そのもの（_BaseColor）に入れる。
        /// 0 に近づくほど透けて消える。
        /// </summary>
        private void SetGlow(Renderer target, Color color, float level)
        {
            _effectBlock.Clear();
            _effectBlock.SetColor(EmissionColorId, color * level);
            _effectBlock.SetColor(BaseColorId, color * level);
            target.SetPropertyBlock(_effectBlock);
        }

        private void Update()
        {
            if (!_exploded || debris == null)
            {
                return;
            }

            _effectTime += Time.deltaTime;
            UpdateEffects();

            float deltaTime = Time.deltaTime;

            for (int i = 0; i < debris.Length; i++)
            {
                Transform piece = debris[i];
                if (piece == null || _resting[i])
                {
                    continue;
                }

                _velocity[i] += Vector3.down * debrisGravity * deltaTime;
                piece.position += _velocity[i] * deltaTime;
                piece.rotation = Quaternion.Euler(_spin[i] * deltaTime) * piece.rotation;

                // 地面：レーンの上ならピン台の上面、外なら背景の地面
                float side = Mathf.Abs(transform.InverseTransformPoint(piece.position).x);
                float ground = transform.TransformPoint(
                    new Vector3(0f, side < laneHalfWidth ? groundHeight : outsideGroundHeight, 0f)).y;

                // 見た目の下端が地面より下へ行かないよう持ち上げて、跳ね返す
                Renderer look = piece.GetComponentInChildren<Renderer>();
                float bottom = look != null ? look.bounds.min.y : piece.position.y;
                if (bottom < ground)
                {
                    piece.position += Vector3.up * (ground - bottom);
                    if (_velocity[i].y < 0f)
                    {
                        _velocity[i].y = -_velocity[i].y * debrisBounce;
                    }

                    _velocity[i].x *= debrisGroundDamping;
                    _velocity[i].z *= debrisGroundDamping;
                    _spin[i] *= debrisGroundDamping;

                    // ほとんど動かなくなったら止める
                    if (_velocity[i].sqrMagnitude < 0.04f)
                    {
                        _resting[i] = true;
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (center == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center.position, entryRadius);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CrazyBowling.Pins
{
    /// <summary>
    /// ピン10本の管理。三角配置、倒れた本数の集計、取り除き、リセット。
    /// スコア計算は段階3で行うため、ここでは本数を持つだけにする。
    /// </summary>
    public class PinSet : MonoBehaviour
    {
        [Header("配置")]
        [Tooltip("並べる対象。手前から 1, 2, 3, 4 列の順に10本入れる。")]
        [SerializeField] private Pin[] pins;

        [Tooltip("横の間隔（m）。実際のボウリングは 0.3048（12インチ）。")]
        [SerializeField] private float pinSpacing = 0.3048f;

        [Tooltip("列の間隔（m）。0.3048 × cos30° = 0.264 で正三角形になる。")]
        [SerializeField] private float rowSpacing = 0.2640f;

        [Tooltip("一番手前のピン（ヘッドピン）の位置（m）。")]
        [SerializeField] private float headPinZ = 16.2f;

        [Tooltip("レーンの中心（m）。")]
        [SerializeField] private float laneCenterX = 0f;

        [Tooltip("ピンを置く高さ（m）。床の上面に合わせる。")]
        [SerializeField] private float baseY = 0.05f;

        [Header("倒れ判定")]
        [Tooltip("この角度を超えて傾いたら倒れたとみなす（度）。")]
        [SerializeField] private float tiltThresholdDegrees = 30f;

        [Tooltip("この高さより下に落ちたら倒れたとみなす（m）。")]
        [SerializeField] private float fallYThreshold = -0.2f;

        [Tooltip("初期位置からこの距離を超えて水平に動いたら倒れたとみなす（m）。立ったまま台から外れた場合を拾う。")]
        [SerializeField] private float horizontalMoveThreshold = 0.3f;

        [Header("静止判定")]
        [Tooltip("これ以下なら静止とみなす速度（m/s）。")]
        [SerializeField] private float restLinearSpeed = 0.05f;

        [Tooltip("これ以下なら静止とみなす角速度（rad/s）。")]
        [SerializeField] private float restAngularSpeed = 0.5f;

        private readonly List<PinSample> _samples = new List<PinSample>();

        /// <summary>連鎖爆発の演出。付いていなければ null。</summary>
        private PinExplosion _explosion;

        /// <summary>ピンの本数。</summary>
        public int PinCount => pins == null ? 0 : pins.Length;

        /// <summary>1投目で倒れた本数。段階3のスコア計算が読む。</summary>
        public int FirstThrowFallen { get; private set; }

        /// <summary>2投目で新たに倒れた本数。段階3のスコア計算が読む。</summary>
        public int SecondThrowFallen { get; private set; }

        /// <summary>このレーンで倒した合計。</summary>
        public int TotalFallen => FirstThrowFallen + SecondThrowFallen;

        private void Awake()
        {
            _explosion = GetComponent<PinExplosion>();
            ApplyLayout();
            ResetAll();
        }

        /// <summary>
        /// Inspector の間隔に従って10本を三角に並べ、その場所を初期姿勢として覚えさせる。
        /// 再生時は Awake から呼ばれる。エディタでは、このコンポーネントを右クリックして実行できる。
        /// </summary>
        [ContextMenu("ピンを並べ直す")]
        public void ApplyLayout()
        {
            if (pins == null)
            {
                return;
            }

            int index = 0;
            // 手前が1本、奥に向かって 2, 3, 4 本
            for (int row = 0; row < 4; row++)
            {
                int countInRow = row + 1;
                float rowZ = headPinZ + rowSpacing * row;

                // その列を中央揃えにする
                float firstX = laneCenterX - pinSpacing * (countInRow - 1) * 0.5f;

                for (int i = 0; i < countInRow; i++)
                {
                    if (index >= pins.Length)
                    {
                        return;
                    }

                    Pin pin = pins[index];
                    index++;
                    if (pin == null)
                    {
                        continue;
                    }

                    Vector3 position = new Vector3(firstX + pinSpacing * i, baseY, rowZ);
                    pin.SetInitialPose(position, Quaternion.identity);
                }
            }
        }

        /// <summary>今、残っているピンのうち倒れている本数。取り除かれたピンは数えない。</summary>
        public int CountFallenNow()
        {
            CollectSamples();
            return PinFallJudge.CountFallen(_samples, BuildSettings());
        }

        /// <summary>残っているピンが全部静止しているか。</summary>
        public bool AreAllAtRest()
        {
            CollectSamples();
            return PinFallJudge.AreAllAtRest(_samples, BuildSettings());
        }

        /// <summary>
        /// 投球の結果を確定して記録する。
        /// 1投目のあとに取り除きを行うので、2投目は自動的に「新たに倒れた本数」になる。
        /// </summary>
        public int JudgeThrow(int throwNumber)
        {
            int fallen = CountFallenNow();
            if (throwNumber <= 1)
            {
                FirstThrowFallen = fallen;
            }
            else
            {
                SecondThrowFallen = fallen;
            }
            return fallen;
        }

        /// <summary>倒れたピンを取り除く（非表示にして物理を止める）。</summary>
        public void RemoveFallen()
        {
            if (pins == null)
            {
                return;
            }

            PinJudgeSettings settings = BuildSettings();
            for (int i = 0; i < pins.Length; i++)
            {
                Pin pin = pins[i];
                if (pin == null || !pin.IsStandingInPlay)
                {
                    continue;
                }

                if (PinFallJudge.IsFallen(pin.CreateSample(), settings))
                {
                    pin.Deactivate();
                }
            }
        }

        /// <summary>
        /// 場外へ飛んだピンを毎ステップ調べて止める。
        /// ピン1本ずつに FixedUpdate を持たせず、ここでまとめて見る。
        /// </summary>
        private void FixedUpdate()
        {
            UpdateCulling();
        }

        /// <summary>
        /// 場外へ飛んだピンを止める。通常は FixedUpdate から呼ばれる。
        /// 物理を手動で進めるとき（検証用）は外から呼べるように公開している。
        /// </summary>
        public void UpdateCulling()
        {
            if (pins == null)
            {
                return;
            }

            for (int i = 0; i < pins.Length; i++)
            {
                if (pins[i] != null && pins[i].IsStandingInPlay)
                {
                    pins[i].UpdateOutOfPlayCulling();
                }
            }
        }

        /// <summary>
        /// 次の投球に備えて、1投ごとの記録（連鎖爆発の発動履歴）を消す。
        /// 位置は動かさないので、2投目に残ったピンはそのまま立っている。
        /// </summary>
        public void PrepareNextThrow()
        {
            if (pins == null)
            {
                return;
            }

            for (int i = 0; i < pins.Length; i++)
            {
                if (pins[i] != null)
                {
                    pins[i].ClearThrowState();
                }
            }

            if (_explosion != null)
            {
                _explosion.ClearThrowState();
            }
        }

        /// <summary>全部のピンを立て直し、本数の記録も消す。</summary>
        public void ResetAll()
        {
            if (pins != null)
            {
                for (int i = 0; i < pins.Length; i++)
                {
                    if (pins[i] != null)
                    {
                        pins[i].ResetToInitial();
                    }
                }
            }

            if (_explosion != null)
            {
                _explosion.ClearThrowState();
            }

            FirstThrowFallen = 0;
            SecondThrowFallen = 0;

            // 位置を書き換えたので、物理側にも即座に反映させる
            Physics.SyncTransforms();
        }

        /// <summary>残っているピンの観測値を集め直す。</summary>
        private void CollectSamples()
        {
            _samples.Clear();
            if (pins == null)
            {
                return;
            }

            for (int i = 0; i < pins.Length; i++)
            {
                Pin pin = pins[i];
                if (pin == null || !pin.IsStandingInPlay)
                {
                    continue;
                }
                _samples.Add(pin.CreateSample());
            }
        }

        /// <summary>Inspector の値を判定用の設定にまとめる。</summary>
        private PinJudgeSettings BuildSettings()
        {
            return new PinJudgeSettings
            {
                tiltThresholdDegrees = tiltThresholdDegrees,
                fallYThreshold = fallYThreshold,
                horizontalMoveThreshold = horizontalMoveThreshold,
                restLinearSpeed = restLinearSpeed,
                restAngularSpeed = restAngularSpeed,
            };
        }
    }
}

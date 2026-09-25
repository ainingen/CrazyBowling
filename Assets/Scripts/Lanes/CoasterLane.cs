using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 宙返りが2回あるレーン（10本目）。ガターも壁も無く、輪の区間は床の無い崖。
    ///
    /// 輪の区間では、ボールを当たり判定ではなく CoasterRail が曲線に乗せて運ぶ。
    /// ここでは毎ステップそれを進め、投球やレーンの出入りで状態を片付けるだけ。
    ///
    /// ★レーンを出るときは、乗っている最中でも必ず降ろす。
    ///   ボールは全レーン共有なので、kinematic と「外から動かしている」の切り替えを
    ///   残したまま次のレーンへ行かせない。
    /// ★基底クラスの OnDestroy は private なので、ここでは上書きしない
    ///   （上書きするとボールの摩擦を戻す処理が呼ばれなくなる。9本目と同じ注意）。
    /// </summary>
    public class CoasterLane : BasicLane
    {
        [Header("宙返り")]
        [Tooltip("輪の区間でボールを運ぶレール。")]
        [SerializeField] private CoasterRail rail;

        [Header("見た目")]
        [Tooltip("ピンに当たった瞬間の閃光（見た目だけ）。空でもよい。")]
        [SerializeField] private CoasterImpactFlash impactFlash;

        public override void OnLaneStart(LaneContext context)
        {
            base.OnLaneStart(context);
            if (rail != null)
            {
                // 輪と床の揺れの位相は、レーンに入るたびに乱数で決める（投げるたびには変えない）
                rail.RandomizePhases();
                rail.ResetState();
            }
        }

        public override void OnThrowStart()
        {
            base.OnThrowStart();
            if (rail != null)
            {
                rail.ResetState();
            }

            // 閃光は投げるたびに1回ぶん用意する（判定・物理には関わらない）
            if (impactFlash != null)
            {
                impactFlash.Arm();
            }
        }

        protected override void OnLaneFixedUpdate(float deltaTime)
        {
            base.OnLaneFixedUpdate(deltaTime);
            if (rail != null)
            {
                rail.Tick(Context.ball, deltaTime);
            }
        }

        public override void OnLaneEnd()
        {
            ReleaseBall();
            base.OnLaneEnd();
        }

        private void OnDisable()
        {
            // レーンごと消されたときの保険
            ReleaseBall();
        }

        private void ReleaseBall()
        {
            if (rail != null)
            {
                rail.ForceRelease(Context.ball);
            }
        }
    }
}

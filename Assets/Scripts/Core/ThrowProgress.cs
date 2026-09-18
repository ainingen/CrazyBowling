namespace CrazyBowling.Core
{
    /// <summary>レーン1本ぶんの投球進行を決めるための設定。</summary>
    public struct ThrowProgressSettings
    {
        /// <summary>このレーンで投げられる回数。通常は2。</summary>
        public int maxThrows;

        /// <summary>立っているピンの本数。通常は10。</summary>
        public int pinCount;
    }

    /// <summary>
    /// 「まだ投げるのか、レーンが終わったのか」を決めるだけの計算。
    /// MonoBehaviour に依存しないので EditMode テストで確かめられる。
    /// </summary>
    public static class ThrowProgress
    {
        /// <summary>
        /// この投球のあと、まだ投げる番があるか。
        /// 全部倒したらそこで終わり（ストライクやスペアのあとは投げない）。
        /// </summary>
        /// <param name="throwNumber">今終わったのが何投目か（1から数える）。</param>
        /// <param name="totalFallen">このレーンで倒した合計本数。</param>
        public static bool HasNextThrow(int throwNumber, int totalFallen, ThrowProgressSettings settings)
        {
            if (totalFallen >= settings.pinCount)
            {
                return false;
            }

            return throwNumber < settings.maxThrows;
        }

        /// <summary>1投目で全部倒したか。</summary>
        public static bool IsStrike(int throwNumber, int totalFallen, ThrowProgressSettings settings)
        {
            return throwNumber == 1 && totalFallen >= settings.pinCount;
        }

        /// <summary>2投目までで全部倒したか（1投目で倒しきった場合は含めない）。</summary>
        public static bool IsSpare(int throwNumber, int totalFallen, ThrowProgressSettings settings)
        {
            return throwNumber >= 2 && totalFallen >= settings.pinCount;
        }
    }
}

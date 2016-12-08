using System.Collections.Generic;

namespace RawNet
{
    internal class CameraSensorInfo
    {
        public int blackLevel;
        public int whiteLevel;
        public int minIso;
        public int maxIso;
        public List<int> mBlackLevelSeparate;

        public CameraSensorInfo(int black_level, int white_level, int min_iso, int max_iso, List<int> black_separate)
        {
            blackLevel = (black_level);
            whiteLevel = (white_level);
            minIso = (min_iso);
            maxIso = (max_iso);
            mBlackLevelSeparate = (black_separate);
        }

        public bool isIsoWithin(int iso)
        {
            return (iso >= minIso && iso <= maxIso) || (iso >= minIso && 0 == maxIso);
        }

        public bool isDefault()
        {
            return (0 == minIso && 0 == maxIso);
        }
    }
}

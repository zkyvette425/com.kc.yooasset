
using UnityEngine;
using YooAsset;

namespace KC
{
    [CreateAssetMenu(menuName = "KC/YooAsset/YooConfig",fileName = "YooConfig",order = 0)]
    public class YooConfig : ScriptableObject
    {
        public EPlayMode playMode;

        public string[] cdn;

        public int useCdnIndex;

        public string CDN => cdn[useCdnIndex];
    }
}


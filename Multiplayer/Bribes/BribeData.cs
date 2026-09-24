using System;

namespace ZooJack
{
    [Serializable]
    public class BribeData
    {
        public string PlayerId;
        public int Amount;
        public bool HasSubmitted;
    }
}

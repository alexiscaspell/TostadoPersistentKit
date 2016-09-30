using System;
using static TostadoPersistentKit.Serializable;

namespace UsingTostadoPersistentKit.TostadoPersistentKit
{
    public class Id:Column
    {
        public PrimaryKeyType type { get; set; }

        public Id()
        {
            type = PrimaryKeyType.SURROGATE;
        }
    }
}

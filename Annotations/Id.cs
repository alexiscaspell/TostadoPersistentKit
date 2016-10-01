using System;
using static TostadoPersistentKit.Serializable;

namespace TostadoPersistentKit
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

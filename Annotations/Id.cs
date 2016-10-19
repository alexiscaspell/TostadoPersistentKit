using System;

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

using System;
using static TostadoPersistentKit.Serializable;

namespace UsingTostadoPersistentKit.TostadoPersistentKit
{
    [AttributeUsage(AttributeTargets.Property)]
    public class Column:MappingAttribute
    {
        public FetchType fetch { get; set; }

        public Column()
        {
            fetch = FetchType.LAZY;
        }
    }
}

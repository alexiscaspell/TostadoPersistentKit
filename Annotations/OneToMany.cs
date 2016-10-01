using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TostadoPersistentKit
{
    public class OneToMany:Column
    {
        public String pkName { get; set; }
        public String tableName { get; set; }
        public String fkName { get; set; }
    }
}

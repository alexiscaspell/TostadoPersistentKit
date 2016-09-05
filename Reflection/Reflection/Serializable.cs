using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TostadoPersistentKit
{
    abstract class Serializable
    {

        internal Dictionary<String, String> mappings = new Dictionary<string, string>();

        //Este metodo inicializa el diccionario mappings, con key=nombre propiedad y value=nombre modelo de datos
        internal abstract void map();

        internal String getMapFromVal(String value)
        {
            return mappings.First(keyValuePair => keyValuePair.Value == value).Key;
        }

        internal String getMapFromKey(String key)
        {
            return mappings[key];
        }

        public Serializable()
        {
            map();
        }
    }
}

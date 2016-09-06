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
            foreach (KeyValuePair<String, String> keyValuePair in mappings)
            {
                if (keyValuePair.Value == value)
                {
                    return keyValuePair.Key;
                }
            }

            return "";
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

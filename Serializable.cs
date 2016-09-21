﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TostadoPersistentKit
{
    public abstract class Serializable
    {

        internal enum PrimaryKeyType { SURROGATE,NATURAL}
        internal enum FetchType { EAGER,LAZY}

        //internal PrimaryKeyType primaryKetyType;

        internal Dictionary<String, String> mappings = new Dictionary<string, string>();

        //Este metodo inicializa el diccionario mappings, con key=nombre propiedad y value=nombre modelo de datos
        internal abstract void map();

        internal abstract string getIdPropertyName();

        internal abstract string getTableName();

        //Setea un enum que indica que tipo de pk es
        internal abstract PrimaryKeyType getPrimaryKeyType();

        internal abstract FetchType getFetchType();

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
            if (mappings.ContainsKey(key))
            {
                return mappings[key];
            }
            return "";
        }

        public Serializable()
        {
            /*setPrimaryKeyType();
            setIdProperty();
            setTableNameProperty();*/
            map();
        }
    }
}

using System;
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

        private Dictionary<String, String> mappings = new Dictionary<string, string>();
        private Dictionary<String, String> oneToMany = new Dictionary<string, string>();
        private Dictionary<String, FetchType> fetchTypes = new Dictionary<string, FetchType>();

        //internal Dictionary<String, String> manyToOne = new Dictionary<string, string>();

        //Este metodo inicializa el diccionario mappings, con key=nombre propiedad y value=nombre modelo de datos
        internal abstract void map();

        internal abstract string getIdPropertyName();

        internal abstract string getTableName();

        //Setea un enum que indica que tipo de pk es
        internal abstract PrimaryKeyType getPrimaryKeyType();

        //internal abstract FetchType getFetchType();

        private String getMapFromVal(Dictionary<string,string> dictionary,String value)
        {
            foreach (KeyValuePair<String, String> keyValuePair in dictionary)
            {
                if (keyValuePair.Value == value)
                {
                    return keyValuePair.Key;
                }
            }

            return "";
        }

        private String getMapFromKey(Dictionary<string,string> dictionary,String key)
        {
            if (dictionary.ContainsKey(key))
            {
                return dictionary[key];
            }
            return "";
        }

        internal String getMapFromVal(String value)
        {
            return getMapFromVal(mappings, value);
        }

        internal String getMapFromKey(String key)
        {
            return getMapFromKey(mappings, key);
        }

        internal String getOneToManyTable(String key)
        {
            string[] result = getMapFromKey(oneToMany, key).Split('.');

            return result.Count() > 1 ? result[1] : "";
        }

        internal String getOneToManyPk(String key)
        {
            string[] result = getMapFromKey(oneToMany, key).Split('.');

            return result.Count() > 0 ? result[0] : "";
        }

        internal String getOneToManyFk(String key)
        {
            string[] result = getMapFromKey(oneToMany, key).Split('.');

            return result.Count() > 2 ? result[2] : "";
        }

        internal bool isOneToManyProperty(string propertyName)
        {
            return oneToMany.ContainsKey(propertyName);
        }

        internal FetchType getFetchType(string propertyName)
        {
            //Como default retorno lazy
            return fetchTypes.ContainsKey(propertyName) ? fetchTypes[propertyName] : FetchType.LAZY;
        }

        internal List<String> getOneToManyPropertyNames()
        {
            return oneToMany.Keys.ToList();
        }

        internal void addMap(String propertyName,String dataName)
        {
            mappings.Add(propertyName, dataName);
        }

        internal void addOneToManyMap(String propertyName, String dataName)
        {
            oneToMany.Add(propertyName, dataName);
        }

        internal void addFetchType(String propertyName,FetchType fetchType)
        {
            fetchTypes.Add(propertyName, fetchType);
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

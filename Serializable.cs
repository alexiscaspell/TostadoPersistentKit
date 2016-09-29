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

        private Dictionary<String, String> mappings = new Dictionary<string, string>();
        private Dictionary<String, String> oneToMany = new Dictionary<string, string>();
        private Dictionary<String, FetchType> fetchTypes = new Dictionary<string, FetchType>();

        
        /// <summary>
        /// en este metodo se ejecutan los metodos addMap,addFetchType y addOneToManyMap
        /// </summary>
        internal abstract void map();

        /// <summary>
        /// retorna el nombre de la propiedad que representa la pk
        /// </summary>
        /// <returns></returns>
        internal abstract string getIdPropertyName();

        /// <summary>
        /// retorna el nombre de la tabla que se mapea en el modelo de datos
        /// </summary>
        /// <returns></returns>
        internal abstract string getTableName();

        /// <summary>
        /// retorna el tipo de pk del modelo de datos
        /// </summary>
        /// <returns></returns>
        internal abstract PrimaryKeyType getPrimaryKeyType();

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

        /// <summary>
        /// agrega el mapeo de una propiedad con una columna de una tabla
        /// </summary>
        /// <param name="propertyName">
        /// nombre de la propiedad</param>
        /// <param name="dataName">
        /// nombre de la columa del modelo de datos</param>
        internal void addMap(String propertyName,String dataName)
        {
            mappings.Add(propertyName, dataName);
        }

        /// <summary>
        ///Agrega relacion oneToMany a cierta propiedad
        /// </summary>
        /// <param name="propertyName">
        /// nombre de la propiedad</param>
        /// <param name="dataName">
        /// esta compuesto de idPk.table.idFk</param>
        internal void addOneToManyMap(String propertyName, String dataName)
        {
            oneToMany.Add(propertyName, dataName);
        }

        /// <summary>
        /// Agrega el tipo de busqueda de una propiedad (por default es lazy)
        /// </summary>
        /// <param name="propertyName">
        /// nombre de la propiedad</param>
        /// <param name="fetchType">
        /// tipod de busqueda</param>
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TostadoPersistentKit
{
    public abstract class Serializable
    {
        private Dictionary<String, String> mappings = new Dictionary<string, string>();
        private Dictionary<String, String> oneToMany = new Dictionary<string, string>();
        private Dictionary<String, FetchType> fetchTypes = new Dictionary<string, FetchType>();
        private String idName;
        private String tableName;
        private PrimaryKeyType primaryKeyType;

        /// <summary>
        /// retorna el nombre de la propiedad que representa la pk
        /// </summary>
        /// <returns></returns>
        internal string getIdPropertyName()
        {
            return idName;
        }

        /// <summary>
        /// retorna el nombre de la tabla que se mapea en el modelo de datos
        /// </summary>
        /// <returns></returns>
        internal string getTableName()
        {
            return tableName;
        }

        /// <summary>
        /// retorna el tipo de pk del modelo de datos
        /// </summary>
        /// <returns></returns>
        internal PrimaryKeyType getPrimaryKeyType()
        {
            return primaryKeyType;
        }

        private String getMapFromVal(Dictionary<string, string> dictionary, String value)
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

        private String getMapFromKey(Dictionary<string, string> dictionary, String key)
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
            string[] result = getMapFromKey(oneToMany, key).Split('@');

            return result.Count() > 1 ? result[1] : "";
        }

        internal String getOneToManyPk(String key)
        {
            string[] result = getMapFromKey(oneToMany, key).Split('@');

            return result.Count() > 0 ? result[0] : "";
        }

        internal String getOneToManyFk(String key)
        {
            string[] result = getMapFromKey(oneToMany, key).Split('@');

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
        [Obsolete]
        internal void addMap(String propertyName, String dataName)
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
        [Obsolete]
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
        [Obsolete]
        internal void addFetchType(String propertyName, FetchType fetchType)
        {
            fetchTypes.Add(propertyName, fetchType);
        }

        public Serializable()
        {
            map();
        }

        private void map()
        {
            List<PropertyInfo> listProperties = GetType().GetProperties().ToList();

            //Recorro todas las propiedades y mapeo las annotations
            foreach (PropertyInfo propertyInfo in listProperties)
            {
                foreach (Attribute attribute in propertyInfo.GetCustomAttributes(false))
                {
                    mapAttributeInformation(propertyInfo.Name, attribute);
                }
            }

            List<CustomAttributeData> classAttributes = GetType().CustomAttributes.ToList();

            if (classAttributes.Exists(a => a.AttributeType == typeof(Table)))//Mapeo nombre de tabla
            {
                string tableNameAnnotatedValue = classAttributes[0].NamedArguments[0].TypedValue.Value.ToString();

                tableName = tableNameAnnotatedValue == "" ? GetType().Name.ToLower() : tableNameAnnotatedValue;
            }
        }

        private void mapAttributeInformation(String propertyName, Attribute annotation)
        {
            if (typeof(Column).IsAssignableFrom(annotation.GetType()))
            {
                if (((Column)annotation).fetch == FetchType.EAGER)
                {
                    fetchTypes.Add(propertyName, FetchType.EAGER);
                }

                if (annotation.GetType() == typeof(OneToMany))
                {
                    OneToMany oneToManyAtt = (OneToMany)annotation;

                    oneToMany.Add(propertyName, oneToManyAtt.pkName + "@" +
                                        oneToManyAtt.tableName + "@" + oneToManyAtt.fkName);
                    return;
                }
            }

            if (annotation.GetType() == typeof(Id))
            {
                Id idAtt = (Id)annotation;

                idName = propertyName;
                primaryKeyType = idAtt.type;
            }

            mappings.Add(propertyName, ((MappingAttribute)annotation).name);
        }

        internal object getDataValue(string dataName)
        {
            List<PropertyInfo> listPropertyInfo = GetType().GetProperties().ToList();

            bool containsProperty = listPropertyInfo.Exists(o => getMapFromKey(o.Name) == dataName);

            if (containsProperty)
            {
                return listPropertyInfo.Find(o => getMapFromKey(o.Name) == dataName).GetValue(this);
            }

            foreach (PropertyInfo item in listPropertyInfo)
            {
                Type propertyType = item.PropertyType;

                if (typeof(Serializable).IsAssignableFrom(propertyType))
                {
                    object propertyInstance = item.GetValue(this);

                    if (propertyInstance != null)
                    {
                        object value = ((Serializable)propertyInstance).getDataValue(dataName);

                        if (value != null)
                        {
                            return value;
                        }
                    }
                }
            }

            return null;
        }

        internal object getPropertyValue(string propertyName)
        {
            List<PropertyInfo> listPropertyInfo = GetType().GetProperties().ToList();

            bool containsProperty = listPropertyInfo.Exists(o => o.Name == propertyName);

            if (containsProperty)
            {
                return listPropertyInfo.Find(o => o.Name == propertyName).GetValue(this);
            }

            foreach (PropertyInfo item in listPropertyInfo)
            {
                Type propertyType = item.PropertyType;

                if (typeof(Serializable).IsAssignableFrom(propertyType))
                {
                    object propertyInstance = item.GetValue(this);

                    if (propertyInstance!=null)
                    {
                        object value = ((Serializable)propertyInstance).getPropertyValue(propertyName);

                        if (value!=null)
                        {
                            return value;
                        }
                    }
                }
            }

            return null;
        }
    }
}

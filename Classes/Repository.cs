using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;

namespace TostadoPersistentKit
{
    public abstract class Repository
    {
        //Esto me dice si se va a mapear sola la clase o a mano, se puede cambiar en cualquier momento
        internal Boolean autoMapping = true;

        /// <summary>
        /// retorna la clase que el repositorio va a tener como modelo para mapear
        /// </summary>
        /// <returns></returns>
        internal abstract Type getModelClassType();

        private List<string> getProcedureParameterNames(String storedProcedure)
        {
            List<string> parameterNames = new List<string>();



            string query = "SELECT * FROM INFORMATION_SCHEMA.PARAMETERS "
                            + "WHERE SPECIFIC_NAME = " + "'"
                            + storedProcedure.Split('.')[storedProcedure.Split('.').Length - 1] + "'"
                            + " ORDER BY SPECIFIC_NAME, ORDINAL_POSITION";

            foreach (Dictionary<string, object> item in DataBase.Instance.ejecutarConsulta(query))
            {
                parameterNames.Add(item["PARAMETER_NAME"].ToString().Split('@')[1]);
            }

            return parameterNames;
        }

        internal IList executeStored(String storedProcedure, Serializable objeto)
        {
            return executeStored(storedProcedure, objeto, new List<SqlParameter>());
        }

        internal IList executeStored(String storedProcedure,Serializable objeto,List<SqlParameter> extraParameters)
        {
            List<string> parameterNames = getProcedureParameterNames(storedProcedure);

            List<SqlParameter> parameters = new List<SqlParameter>();

            extraParameters.ForEach(o => parameterNames.Remove(o.ParameterName));

            extraParameters.ForEach(o => parameters.Add(o));

            foreach (string parameterName in parameterNames)
            {
                object dataValue = objeto.getDataValue(parameterName);

                if (dataValue!=null)
                {
                    if (typeof(Serializable).IsAssignableFrom(dataValue.GetType()))
                    {
                        Serializable serializableProperty = (Serializable)dataValue;
                        dataValue = serializableProperty.getDataValue(serializableProperty
                                                            .getMapFromKey(serializableProperty
                                                            .getIdPropertyName()));
                    }

                }

                DataBase.Instance.agregarParametro(parameters, parameterName, dataValue);
            }

            return executeStored(storedProcedure, parameters);
        }

        internal IList executeStored(String storedProcedure,List<SqlParameter> parameters)
        {
            return executeStored(storedProcedure,parameters,getModelClassType());
        }

        private IList executeStored(String storedProcedure, List<SqlParameter> parameters, Type modelClassType)
        {
            List<Dictionary<string, object>> dictionaryList = DataBase.Instance.ejecutarStoredProcedure(storedProcedure, parameters);

            return returnValue(dictionaryList, modelClassType);
        }

        internal IList executeQuery(String query, List<SqlParameter> parameters)
        {
            return executeQuery(query, parameters, getModelClassType());
        }

        private IList executeQuery(String query, List<SqlParameter> parameters,Type modelClassType)
        {
            List<Dictionary<string, object>> dictionaryList = DataBase.Instance.ejecutarConsulta(query, parameters);

            return returnValue(dictionaryList,modelClassType);
        }

        private void completeSerializableObject(Serializable incompleteObject)
        {
            foreach (KeyValuePair<string,object> item in getPropertyValues(incompleteObject,true))
            {
                if (incompleteObject.getFetchType(item.Key)==FetchType.EAGER)
                {
                    if (incompleteObject.isOneToManyProperty(item.Key))
                    {
                        completeOneToManyProperty(incompleteObject, item.Key);
                    }

                    if (typeof(Serializable).IsAssignableFrom(incompleteObject.GetType().GetProperty(item.Key).PropertyType)&&item.Value!=null)
                    {
                        Serializable serializableProperty = (Serializable)item.Value;

                        Type propertyType = serializableProperty.GetType();

                        object propertyId = propertyType.GetProperty(serializableProperty.
                                                    getIdPropertyName()).GetValue(serializableProperty);


                        serializableProperty = (Serializable)selectById(propertyId, propertyType);

                        incompleteObject.GetType().GetProperty(item.Key).SetValue(incompleteObject, serializableProperty);
                    }
                }
            }
        }

        private void completeOneToManyProperty(Serializable incompleteObject, string propertyName)
        {
            Type containingTypeOfProperty = Assembly.GetExecutingAssembly().
                                            GetType(incompleteObject.GetType().
                                            GetProperty(propertyName).PropertyType.
                                            ToString().Split('[')[1].Split(']')[0]);

            Serializable containingTypeOfPropertyInstance = (Serializable)Activator.CreateInstance(containingTypeOfProperty);

            string intermediateTable = incompleteObject.getOneToManyTable(propertyName);
            string currentForeignKey = incompleteObject.getOneToManyFk(propertyName);
            string currentPrimaryKey = incompleteObject.getOneToManyPk(propertyName);

            object currentIdValue = incompleteObject.GetType().GetProperty(incompleteObject.getIdPropertyName()).
                                    GetValue(incompleteObject);

            string expected = "@"+currentPrimaryKey;
            List<SqlParameter> sqlParameters = new List<SqlParameter>();
            DataBase.Instance.agregarParametro(sqlParameters, expected, currentIdValue);

            //bool isCharObject = typeof(string).IsAssignableFrom(currentIdValue.GetType()) || typeof(char).IsAssignableFrom(currentIdValue.GetType());

            //string expected = isCharObject ? "'" + currentIdValue.ToString() + "'" : currentIdValue.ToString();


            string query = "select * from " + intermediateTable + " ";
            string conditionQuery = "where " + currentPrimaryKey + "=" + expected;

            if (containingTypeOfPropertyInstance.getTableName()!=intermediateTable)
            {
                query += "inner join " + containingTypeOfPropertyInstance.getTableName() +
                        " on(" + intermediateTable + "." + currentForeignKey + "=" + 
                        containingTypeOfPropertyInstance.getTableName()+"." +
                        containingTypeOfPropertyInstance.
                        getMapFromKey(containingTypeOfPropertyInstance.getIdPropertyName()) + ") ";
            }

            query += conditionQuery;

            Type listType = incompleteObject.GetType().GetProperty(propertyName).PropertyType;

            object dummyList = Activator.CreateInstance(listType);

            //Asigno una lista vacia a la propiedad
            incompleteObject.GetType().GetProperty(propertyName).
                            SetValue(incompleteObject, dummyList);

            foreach (var item in (List<object>)executeQuery(query, sqlParameters, containingTypeOfProperty))
            {
                List<object> parameters = new List<object> { item};
                incompleteObject.GetType().GetProperty(propertyName).
                                PropertyType.GetMethod("Add").
                                Invoke(incompleteObject.GetType().
                                GetProperty(propertyName).
                                GetValue(incompleteObject), parameters.ToArray());
            }
        }

        private IList returnValue(List<Dictionary<string,object>> dictionaryList,Type modelClassType)
        {
            if (autoMapping)
            {
                IList mappedList = getTypeList(modelClassType);

                dictionaryList.ForEach(dictionary => mappedList.Add(unSerialize(dictionary,modelClassType)));

                foreach (var item in mappedList)
                {
                    completeSerializableObject((Serializable)item);
                }

                return mappedList;
            }
            else
            {
                return dictionaryList;
            }
        }

        internal object unSerialize(Dictionary<string, object> dictionary)
        {
            return unSerialize(dictionary, getModelClassType());
        }

        private object unSerialize(Dictionary<string, object> dictionary,Type modelClassType)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(modelClassType);

            Dictionary<string, object> dictionaryAux = copyDictionary(dictionary);

            foreach (String dataName in dictionary.Keys)
            {
                String propertyName = objeto.getMapFromVal(dataName);
                String keyToRemove = dataName;

                if (propertyName != "")
                {
                    Type propertyType = objeto.GetType().GetProperty(propertyName).PropertyType;

                    bool isSerializable = typeof(Serializable).IsAssignableFrom(propertyType);

                    if (dictionary[dataName].ToString() != "")//Esto esta hardcodeado para que no setee cosas en null
                    {
                        if (isSerializable)
                        {
                            Serializable propertyInstance = (Serializable)Activator.CreateInstance(propertyType);
                            keyToRemove = propertyInstance.getMapFromKey(propertyInstance.getIdPropertyName());

                            object dataValue = dictionaryAux[dataName];
                            dictionaryAux.Remove(dataName);
                            dictionaryAux.Add(keyToRemove, dataValue);

                            objeto.GetType().GetProperty(propertyName).SetValue(objeto, unSerialize(dictionaryAux, propertyType));
                        }

                        else
                        {
                            object dataValue = getCastedValue(dictionary[dataName], objeto.GetType().GetProperty(propertyName).PropertyType);
                            objeto.GetType().GetProperty(propertyName).SetValue(objeto, dataValue);
                        }
                        dictionaryAux.Remove(keyToRemove);
                    }
                }
            }

            return objeto;
        }

        private object getCastedValue(object value, Type expectedType)
        {
            return Convert.ChangeType(value, expectedType);
        }

        private Dictionary<string, object> copyDictionary(Dictionary<string, object> dictionary)
        {
            Dictionary<string, object> copyDictionary = new Dictionary<string, object>();

            foreach (KeyValuePair<string,object> keyValuePair in dictionary)
            {
                copyDictionary.Add(keyValuePair.Key, keyValuePair.Value);
            }

            return copyDictionary;
        }

        private List<String> listSerializableProperties(object objeto)
        {
            List<String> serializableProperties = new List<string>();

            foreach (MemberInfo info in objeto.GetType().GetMembers())
            {
                if (info.MemberType==MemberTypes.Property)
                {
                    Type propertyType = ((PropertyInfo)info).PropertyType;

                    bool isSerializable = typeof(Serializable).IsAssignableFrom(propertyType);
                    if (isSerializable)
                    {
                        serializableProperties.Add(((PropertyInfo)info).Name);
                    }
                }
            }

            return serializableProperties;
        }

        private Dictionary<string, object> getPropertyValues(Serializable objeto)
        {
            return getPropertyValues(objeto, false);
        }

        private Dictionary<string,object> getPropertyValues(Serializable objeto,bool nullValuesPermited)
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>();

            foreach (MemberInfo info in objeto.GetType().GetMembers())
            {
                if (info.MemberType == MemberTypes.Property)
                {
                    string propertyName = ((PropertyInfo)info).Name;

                    object propertyValue = ((PropertyInfo)info).GetValue(objeto);

                    if (propertyValue!=null||nullValuesPermited)
                    {
                        dictionary.Add(propertyName, propertyValue);
                    }
                }
            }

            return dictionary;
        }

        internal void insert(Serializable objeto)
        {
            insert(objeto,objeto.getIdPropertyName(), objeto.getTableName(),false);
        }

        internal void insertCascade(Serializable objeto)
        {
            insert(objeto, objeto.getIdPropertyName(), objeto.getTableName(), true);
        }

        private void insert(Serializable objeto, String primaryKeyPropertyName, String tableName,bool cascade)
        {
            String insertQuery = "insert into " + tableName + "(";

            String valuesString = " values (";

            List<SqlParameter> parametros = new List<SqlParameter>();

            Dictionary<string, object> propertyValues = getPropertyValues(objeto);

            foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
            {
                if (keyValuePair.Key != primaryKeyPropertyName || objeto.getPrimaryKeyType() == PrimaryKeyType.NATURAL)
                {
                    String dataName = objeto.getMapFromKey(keyValuePair.Key);

                    if (dataName!="")
                    {
                        insertQuery += dataName + ",";

                        valuesString += "@" + dataName + ",";

                        bool isSerializableProperty = typeof(Serializable).IsAssignableFrom(keyValuePair.Value.GetType());

                        Serializable serializableProperty = isSerializableProperty ? (Serializable)keyValuePair.Value : null;

                        if (cascade && isSerializableProperty)
                        {
                            insert(serializableProperty, serializableProperty.getIdPropertyName(), serializableProperty.getTableName(), true);
                        }

                        object parametro = isSerializableProperty ? serializableProperty.GetType().
                                            GetProperty(serializableProperty.getIdPropertyName()).
                                            GetValue(serializableProperty) : keyValuePair.Value;

                        DataBase.Instance.agregarParametro(parametros, "@" + dataName, parametro);
                    }
                }
            }

            string primaryKeyName = objeto.getMapFromKey(objeto.getIdPropertyName());

            insertQuery = insertQuery.Remove(insertQuery.Length - 1);
            valuesString = valuesString.Remove(valuesString.Length - 1);
            insertQuery += ") " + "output inserted."+ primaryKeyName
                               + valuesString + ")";

            object insertResult = DataBase.Instance.ejecutarConsulta(
                                  insertQuery, parametros)[0][primaryKeyName];

            Type idType = objeto.GetType().GetProperty(objeto.getIdPropertyName()).PropertyType;

            object idValue = getCastedValue(insertResult, idType);

            objeto.GetType().GetProperty(objeto.getIdPropertyName()).SetValue(objeto, idValue);

            foreach (var item in objeto.getOneToManyPropertyNames())
            {
                insertOneToManyProperty(objeto, item,cascade);
            }
        }

        private void insertOneToManyProperty(Serializable objeto, string propertyName,bool cascade)
        {
            Type propertyType = objeto.getOneToManyPropertyType(propertyName);

            string propertyTable = ((Serializable)Activator.CreateInstance(propertyType)).getTableName();

            string oneToManyTable = objeto.getOneToManyTable(propertyName);

            //bool isCharPk = typeof(string).IsAssignableFrom(objeto.getPropertyValue(objeto.getIdPropertyName()).GetType()) 
              //              || typeof(char).IsAssignableFrom(objeto.getPropertyValue(objeto.getIdPropertyName()).GetType());

            //string expectedPk = isCharPk ? "'" + objeto.getPropertyValue(objeto.getIdPropertyName()).ToString() + "'" : 
                                //objeto.getPropertyValue(objeto.getIdPropertyName()).ToString();

            string expectedPk = "@" + objeto.getOneToManyPk(propertyName);

            foreach (var item in (IEnumerable)objeto.getPropertyValue(propertyName))
            {
                Serializable serializableItem = (Serializable)item;

                //bool isCharFk = typeof(string).IsAssignableFrom(serializableItem.getPropertyValue(serializableItem.getIdPropertyName()).GetType())
                                //|| typeof(char).IsAssignableFrom(serializableItem.getPropertyValue(serializableItem.getIdPropertyName()).GetType());

                //string expectedFk = isCharPk ? "'" + serializableItem.getPropertyValue(serializableItem.getIdPropertyName()).ToString() + "'" :
                                    //serializableItem.getPropertyValue(serializableItem.getIdPropertyName()).ToString();
                string expectedFk = "@" + objeto.getOneToManyFk(propertyName);

                List<SqlParameter> sqlParameters = new List<SqlParameter>();
                DataBase.Instance.agregarParametro(sqlParameters, expectedPk, objeto.getPropertyValue(objeto.getIdPropertyName()));
                DataBase.Instance.agregarParametro(sqlParameters, expectedFk, serializableItem.getPropertyValue(serializableItem.getIdPropertyName()));

                string query = "";

                if (propertyTable==oneToManyTable)
                {
                    if (cascade)
                    {
                        insert(serializableItem, serializableItem.getIdPropertyName(), propertyTable, cascade);
                    }

                    query = "update " + propertyTable + " set " + objeto.getOneToManyPk(propertyName) + "=" + expectedPk +
                                        "where " + objeto.getOneToManyFk(propertyName) + "=" + expectedFk;
                }
                else
                {
                    query = "insert into " + oneToManyTable + "(" + objeto.getOneToManyPk(propertyName) + 
                            "," + objeto.getOneToManyFk(propertyName) + ") values(" + expectedPk + "," + 
                            expectedFk + ")";
                }

                DataBase.Instance.ejecutarConsulta(query);
            }
        }

        internal void delete(Serializable objeto)
        {
            delete(objeto, objeto.getIdPropertyName(), objeto.getTableName());
        }

        private void delete(Serializable objeto,String primaryKeyPropertyName,String tableName)
        {
            String deleteQuery = "delete from " + tableName + " where " 
                                + objeto.getMapFromKey(primaryKeyPropertyName) + "="
                                + objeto.GetType().GetProperty(primaryKeyPropertyName).GetValue(objeto).ToString();

            DataBase.Instance.ejecutarConsulta(deleteQuery);
        }

        //Se supone que lo que se espera en el where de la consulta es id.toString()
        internal object selectById(object id)
        {
            return selectById(id, getModelClassType());
        }

        private object selectById(object id,Type classType)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(classType);

            IList result = selectByProperty(objeto.getIdPropertyName(), id,classType);

            return (result.Count > 0) ? result[0] : null;
        }

        internal IList selectByProperties(Dictionary<string, object> properties)
        {
            return selectByProperties(properties, getModelClassType());
        }

        internal IList selectByProperties(Dictionary<string,object> properties,Type classType)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(classType);

            List<SqlParameter> parameters = new List<SqlParameter>();

            string selectQuery = "select * from " + objeto.getTableName() + " where ";

            foreach (KeyValuePair<string,object> property in properties)
            {
                string expected = "@" + objeto.getMapFromKey(property.Key);
                DataBase.Instance.agregarParametro(parameters, expected, property.Value);
                selectQuery += objeto.getMapFromKey(property.Key) + "=" + expected + " and ";
            }

            selectQuery = selectQuery.Remove(selectQuery.Length - 5);

            return executeAutoMappedSelect(selectQuery, parameters, classType);
        }

        internal IList selectByProperty(string propertyName,object propertyValue)
        {
            return selectByProperty(propertyName, propertyValue, getModelClassType());
        }

        internal IList selectByProperty(string propertyName, object propertyValue, Type classType)
        {
            Dictionary<string,object> properties = new Dictionary<string, object>();
            properties.Add(propertyName, propertyValue);

            return selectByProperties(properties,classType);
        }

        private IList executeAutoMappedSelect(String selectQuery,List<SqlParameter> parameters,Type classType)
        {
            bool actualAutoMappingVal = autoMapping;//guardo el valor actual de autoMapping

            autoMapping = true;

            IList result = executeQuery(selectQuery, parameters,classType);

            autoMapping = actualAutoMappingVal;

            return result;
        }

        internal IList selectAll()
        {
            return selectAll(getModelClassType());
        }

        internal IList selectAll(Type classType)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(classType);

            String tableName = objeto.getTableName();

            List<SqlParameter> parameters = new List<SqlParameter>();

            String selectQuery = "select * from " + tableName;

            return executeAutoMappedSelect(selectQuery, parameters,classType);
        }

        internal void update(Serializable objeto)
        {
            update(objeto, objeto.getIdPropertyName(), objeto.getTableName(),false);
        }

        //No setea valores null
        //Si se modifico la pk aunque sea natural no la updatea (usea ignora la pk)
        private void update(Serializable objeto, String primaryKeyPropertyName, String tableName, bool cascadeMode)
        {
            String updateQuery = "update " + tableName + " set ";

            List<SqlParameter> parametros = new List<SqlParameter>();

            Dictionary<string, object> propertyValues = getPropertyValues(objeto);

            foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
            {
                if (keyValuePair.Key != primaryKeyPropertyName)
                {
                    String dataName = objeto.getMapFromKey(keyValuePair.Key);

                    if (dataName!="")
                    {
                        bool isSerializableProperty = typeof(Serializable).IsAssignableFrom(keyValuePair.Value.GetType());

                        object parametro = keyValuePair.Value;

                        if (isSerializableProperty)
                        {
                            Serializable serializableProperty = (Serializable)keyValuePair.Value;

                            parametro = serializableProperty.GetType().
                                            GetProperty(serializableProperty.getIdPropertyName()).
                                            GetValue(serializableProperty);
                        }
                        updateQuery += dataName + "=@" + dataName + ",";
                        DataBase.Instance.agregarParametro(parametros, "@" + dataName, parametro);
                    }
                }
            }

            updateQuery = updateQuery.Remove(updateQuery.Length - 1);

            updateQuery += " where " + objeto.getMapFromKey(primaryKeyPropertyName) + "="
                        + objeto.GetType().GetProperty(primaryKeyPropertyName).GetValue(objeto);

            if (cascadeMode)
            {
                List<String> serializablePropertyNames = listSerializableProperties(objeto);

                foreach (KeyValuePair<string, object> keyValuePair in propertyValues)
                {
                    //Aca supongo que no puede ser null
                    if (serializablePropertyNames.Contains(keyValuePair.Key))
                    {
                        Serializable serializableProperty = (Serializable)keyValuePair.Value;

                        update(serializableProperty, serializableProperty.getIdPropertyName(), serializableProperty.getTableName(), cascadeMode);
                    }
                }
            }

            DataBase.Instance.ejecutarConsulta(updateQuery, parametros);

            foreach (var item in objeto.getOneToManyPropertyNames())
            {
                updateOneToManyProperty(objeto, item, cascadeMode);
            }
        }

        private void updateOneToManyProperty(Serializable objeto, string propertyName, bool cascadeMode)
        {
            object propertyList = objeto.getPropertyValue(propertyName);

            //SI es cascade updateo cada elemento de la propiedad oneToMany
            if (cascadeMode)
            {
                foreach (var item in (IEnumerable)propertyList)
                {
                    updateCascade((Serializable)item);
                }
            }

            object notExistentFkObjects = Activator.CreateInstance(objeto.getPropertyType(propertyName));
            List<object> existentFks = new List<object>();

            //bool isCharObject = typeof(string).IsAssignableFrom(objeto.getPropertyType(objeto.getIdPropertyName())) || typeof(char).IsAssignableFrom(objeto.getPropertyType(objeto.getIdPropertyName()));

            //string expected = isCharObject ? "'" + objeto.getPropertyValue(objeto.getIdPropertyName()).ToString() + "'" : objeto.getPropertyValue(objeto.getIdPropertyName()).ToString();

            string expected = "@" + objeto.getOneToManyPk(propertyName);
            List<SqlParameter> sqlParameters = new List<SqlParameter>();
            DataBase.Instance.agregarParametro(sqlParameters, expected, objeto.getPropertyValue(objeto.getIdPropertyName()));

            string existentFksQuery = "select " + objeto.getOneToManyFk(propertyName)
                                    + " from " + objeto.getOneToManyTable(propertyName) + " where " 
                                    + objeto.getOneToManyPk(propertyName) + "=" + expected;

            foreach (Dictionary<string,object> item in DataBase.Instance.ejecutarConsulta(existentFksQuery,sqlParameters))
            {
                existentFks.Add(item[objeto.getOneToManyFk(propertyName)]);
            }

            foreach (var item in (IEnumerable)propertyList)
            {
                if (!existentFks.Contains(((Serializable)item).getPropertyValue(((Serializable)item).getIdPropertyName())))
                {
                    List<object> parameters = new List<object> { item };
                    notExistentFkObjects.GetType().GetMethod("Add").Invoke(notExistentFkObjects, parameters.ToArray());
                }
            }

            //Seteo lista filtrada
            objeto.GetType().GetProperty(propertyName).SetValue(objeto,notExistentFkObjects);

            insertOneToManyProperty(objeto, propertyName, false);
            //Dejo todo como estaba
            objeto.GetType().GetProperty(propertyName).SetValue(objeto, propertyList);
        }

        internal void updateCascade(Serializable objeto)
        {
            update(objeto, objeto.getIdPropertyName(), objeto.getTableName(),true);
        }

        private IList getTypeList(Type type)
        {
            Type listType = typeof(List<>).MakeGenericType(new[] { type });
            return (IList)Activator.CreateInstance(listType);
        }
    }
}

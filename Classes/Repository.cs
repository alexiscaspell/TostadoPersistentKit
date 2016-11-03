using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
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
                        dataValue = serializableProperty.getPkValue();
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

        internal void completeProperty(string propertyName,Serializable incompleteObject)
        {
            completeProperty(incompleteObject, propertyName, incompleteObject.getPropertyValue(propertyName), false);
        }

        internal void completeProperty(Serializable incompleteObject,string propertyName,object propertyValue,bool ignoreLazyProperty)
        {
            if (incompleteObject.getFetchType(propertyName) == FetchType.EAGER||!ignoreLazyProperty)
            {
                if (incompleteObject.isOneToManyProperty(propertyName))
                {
                    completeOneToManyProperty(incompleteObject, propertyName);
                }

                if (typeof(Serializable).IsAssignableFrom(incompleteObject.getPropertyType(propertyName)) && propertyValue != null)
                {
                    Serializable serializableProperty = (Serializable)propertyValue;

                    Type propertyType = serializableProperty.GetType();

                    object propertyId = serializableProperty.getPkValue();

                    serializableProperty = (Serializable)selectById(propertyId, propertyType);

                    incompleteObject.GetType().GetProperty(propertyName).SetValue(incompleteObject, serializableProperty);
                }
            }
        }

        private void completeSerializableObject(Serializable incompleteObject)
        {
            foreach (KeyValuePair<string,object> item in getPropertyValues(incompleteObject,true))
            {
                completeProperty(incompleteObject, item.Key, item.Value, true);
            }
        }

        private void completeOneToManyProperty(Serializable incompleteObject, string propertyName)
        {
            Type containingTypeOfProperty = incompleteObject.getOneToManyPropertyType(propertyName);

            Serializable containingTypeOfPropertyInstance = (Serializable)Activator.CreateInstance(containingTypeOfProperty);

            string intermediateTable = incompleteObject.getOneToManyTable(propertyName);
            string currentForeignKey = incompleteObject.getOneToManyFk(propertyName);
            string currentPrimaryKey = incompleteObject.getOneToManyPk(propertyName);

            object currentIdValue = incompleteObject.getPkValue();

            string expected = "@"+currentPrimaryKey;
            List<SqlParameter> sqlParameters = new List<SqlParameter>();
            DataBase.Instance.agregarParametro(sqlParameters, expected, currentIdValue);

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

            Type listType = incompleteObject.getPropertyType(propertyName);

            object dummyList = Activator.CreateInstance(listType);

            //Asigno una lista vacia a la propiedad
            incompleteObject.GetType().GetProperty(propertyName).
                            SetValue(incompleteObject, dummyList);

            foreach (var item in executeQuery(query, sqlParameters, containingTypeOfProperty))
            {
                List<object> parameters = new List<object> { item};
                incompleteObject.GetType().GetProperty(propertyName).
                                PropertyType.GetMethod("Add").
                                Invoke(incompleteObject.getPropertyValue(propertyName), parameters.ToArray());
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
                    Type propertyType = objeto.getPropertyType(propertyName);

                    bool isSerializable = typeof(Serializable).IsAssignableFrom(propertyType);

                    if (dictionary[dataName].ToString() != "")//Esto esta hardcodeado para que no setee cosas en null
                    {
                        if (isSerializable)
                        {
                            Serializable propertyInstance = (Serializable)Activator.CreateInstance(propertyType);
                            keyToRemove = propertyInstance.getMapFromKey(propertyInstance.getIdPropertyName());

                            object dataValue = dictionaryAux[dataName];
                            dictionaryAux.Remove(dataName);

                            if (!dictionaryAux.ContainsKey(keyToRemove))//parchado porque rompio en un test diciendo que ya se habia insertado la clave
                            {
                                dictionaryAux.Add(keyToRemove, dataValue);
                            }

                            objeto.GetType().GetProperty(propertyName).SetValue(objeto, unSerialize(dictionaryAux, propertyType));
                        }

                        else
                        {
                            object dataValue = getCastedValue(dictionary[dataName], objeto.getPropertyType(propertyName));
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

                        object parametro = isSerializableProperty ? serializableProperty.getPropertyValue(
                                            serializableProperty.getIdPropertyName()) : keyValuePair.Value;

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

            Type idType = objeto.getPropertyType(objeto.getIdPropertyName());

            object idValue = null;

            if (typeof(Serializable).IsAssignableFrom(idType))
            {
                idValue = objeto.getIdValue();
                Serializable serializableId = (Serializable)idValue;
                serializableId.GetType().GetProperty(serializableId.getIdPropertyName()).
                                        SetValue(serializableId, 
                                        getCastedValue(insertResult, 
                                         serializableId.
                                         getPropertyType(serializableId.getIdPropertyName())));
            }
            else
            {
                idValue = getCastedValue(insertResult, idType);
            }

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

            string expectedPk = "@" + objeto.getOneToManyPk(propertyName);

            foreach (var item in (IEnumerable)objeto.getPropertyValue(propertyName))
            {
                Serializable serializableItem = (Serializable)item;
                
                string expectedFk = "@" + objeto.getOneToManyFk(propertyName);

                List<SqlParameter> sqlParameters = new List<SqlParameter>();
                DataBase.Instance.agregarParametro(sqlParameters, expectedPk, objeto.getPkValue());
                DataBase.Instance.agregarParametro(sqlParameters, expectedFk, serializableItem.getPkValue());

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

                DataBase.Instance.ejecutarConsulta(query,sqlParameters);
            }
        }

        internal void delete(Serializable objeto)
        {
            delete(objeto, objeto.getIdPropertyName(), objeto.getTableName());
        }

        private void delete(Serializable objeto,String primaryKeyPropertyName,String tableName)
        {
            List<SqlParameter> parameters = new List<SqlParameter>();

            string expected = "@" + objeto.getMapFromKey(primaryKeyPropertyName);

            DataBase.Instance.agregarParametro(parameters, expected, objeto.getPkValue());

            String deleteQuery = "delete from " + tableName + " where "
                                + objeto.getMapFromKey(primaryKeyPropertyName) + "="
                                + expected;

            DataBase.Instance.ejecutarConsulta(deleteQuery,parameters);
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
                object pkPropertyValue = typeof(Serializable).IsAssignableFrom(property.Value.GetType()) ? ((Serializable)property.Value).getPkValue() : property.Value;

                DataBase.Instance.agregarParametro(parameters, expected, pkPropertyValue);
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

                            parametro = serializableProperty.getPropertyValue(serializableProperty.getIdPropertyName());
                        }
                        updateQuery += dataName + "=@" + dataName + ",";
                        DataBase.Instance.agregarParametro(parametros, "@" + dataName, parametro);
                    }
                }
            }

            updateQuery = updateQuery.Remove(updateQuery.Length - 1);

            updateQuery += " where " + objeto.getMapFromKey(primaryKeyPropertyName) + "="
                        + objeto.getPkValue();

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

            string expected = "@" + objeto.getOneToManyPk(propertyName);
            List<SqlParameter> sqlParameters = new List<SqlParameter>();
            DataBase.Instance.agregarParametro(sqlParameters, expected, objeto.getPkValue());

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

        internal IList executeDynamicQuery(params object[] parameters)
        {
            /*StackFrame frame = new StackFrame(1);
            var method = frame.GetMethod();
            var type = method.DeclaringType;
            var name = method.Name;*/
            string methodName = new StackFrame(1).GetMethod().Name;

            List<Dictionary<string, string>> columnsAndConnectors = getParsedQueryMethod(methodName);

            List<SqlParameter> sqlParameters = new List<SqlParameter>();

            string query = "select * from " + ((Serializable)Activator.CreateInstance(getModelClassType())).getTableName() + " where";



            for (int i = 0; i < columnsAndConnectors.Count; i++)
            {
                Dictionary<string, string> item = columnsAndConnectors[i];

                int tries = 0;
                bool error = true;

                while (error)
                {
                    if (!sqlParameters.Exists(p => p.ParameterName == "@" + item["column"] + tries.ToString()))
                    {
                        error = false;
                        DataBase.Instance.agregarParametro(sqlParameters, "@" + item["column"] + tries.ToString(), parameters[i]);
                        tries--;
                    }
                    tries++;
                }

                query += " " + item["column"] + "=@" + item["column"]+tries.ToString() + " " + item["connector"];
            }

            return executeQuery(query, sqlParameters);
        }

        private List<Dictionary<string, string>> getParsedQueryMethod(string methodName)
        {
            Serializable objeto = (Serializable)Activator.CreateInstance(getModelClassType());

            List<Dictionary<string, string>> columnsAndConnectors = new List<Dictionary<string, string>>();

            string unparsedQuery = methodName.Remove(0, 8);//methodName.Split(new string[] { "By" }, StringSplitOptions.None)[1];

            List<char> enumerableUnparsedQuery = unparsedQuery.ToList();

            int counter = 0;

            for (int i = 0; i < enumerableUnparsedQuery.Count; i++)
            {
                Dictionary<string, string> newDictionary = new Dictionary<string, string>();

                string column = "";
                string property = "";

                if (i+3>=enumerableUnparsedQuery.Count)
                {
                    property = unparsedQuery.Substring(counter, unparsedQuery.Length-counter);//unparsedQuery;

                    column = property;
                    char[] dummyArray = column.ToCharArray();
                    dummyArray[0] = property[0].ToString().ToLower()[0];
                    column = new string(dummyArray);
                    column = objeto.getMapFromKey(column);

                    if (column=="")
                    {
                        column = objeto.getMapFromKey(property);
                    }

                    newDictionary.Add("column", column);
                    newDictionary.Add("connector", "");
                    columnsAndConnectors.Add(newDictionary);
                    break;
                }
                if (enumerableUnparsedQuery[i]=='O'&&enumerableUnparsedQuery[i+1]=='r')
                {
                    property = unparsedQuery.Substring(counter, i-counter);//unparsedQuery.Remove(i, unparsedQuery.Length - i);
                    counter += property.Length+ 2;

                    column = property;
                    char[] dummyArray = column.ToCharArray();
                    dummyArray[0] = property[0].ToString().ToLower()[0];
                    column = new string(dummyArray);
                    column = objeto.getMapFromKey(column);

                    if (column == "")
                    {
                        column = objeto.getMapFromKey(property);
                    }

                    newDictionary.Add("column", column);
                    newDictionary.Add("connector", "or");
                    //unparsedQuery = unparsedQuery.Remove(0, i + 2);
                    columnsAndConnectors.Add(newDictionary);
                }
                if (enumerableUnparsedQuery[i] == 'A' && enumerableUnparsedQuery[i + 1] == 'n'&&enumerableUnparsedQuery[i+2]=='d')
                {
                    property = unparsedQuery.Substring(counter, i-counter);//unparsedQuery.Remove(i, unparsedQuery.Length - i);
                    counter += property.Length + 3;

                    column = property;
                    char[] dummyArray = column.ToCharArray();
                    dummyArray[0] = property[0].ToString().ToLower()[0];
                    column = new string(dummyArray);
                    column = objeto.getMapFromKey(column);

                    if (column == "")
                    {
                        column = objeto.getMapFromKey(property);
                    }

                    newDictionary.Add("column", column);
                    newDictionary.Add("connector", "and");
                    //unparsedQuery = unparsedQuery.Remove(0, i + 3);
                    columnsAndConnectors.Add(newDictionary);
                }
            }

            return columnsAndConnectors;
        }
    }
}

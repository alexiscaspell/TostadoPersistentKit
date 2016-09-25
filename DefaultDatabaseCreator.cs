using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TostadoPersistentKit;

namespace UsingTostadoPersistentKit.TostadoPersistentKit
{
    public class DefaultDatabaseCreator
    {

        private Dictionary<Type, Serializable> dictionaryObjectsAndTypes = new Dictionary<Type, Serializable>();

        public DefaultDatabaseCreator()
        {
            loadObjectsAndTypes();
        }

        public void createPersistentDefaultModel(bool deleteExistingTables)
        {
            if (deleteExistingTables)
            {
                dropExistingTables();
            }

            if (dictionaryObjectsAndTypes.Values.Any(objeto => existsTable(objeto.getTableName())))
            {
                return;//Si existe alguna de las tablas no creo nada
            }

            createIncompleteTables();
            createForeignKeys();
        }

        public void dropExistingTables()
        {
            //Este while sirve para ir borrando las tablas varias veces, porque algunas no
            //se borran a la primera por las fks
            while (dictionaryObjectsAndTypes.Values.Any(o=>existsTable(o.getTableName())))
            {
                foreach (var item in dictionaryObjectsAndTypes.Values)
                {
                    if (existsTable(item.getTableName()))
                    {
                        dropTable(item.getTableName());
                    }
                }
            }
        }

        private void dropTable(string table)
        {
            string dropQuery = "drop table " + table;

            try
            {
                DataBase.Instance.ejecutarConsulta(dropQuery);
            }
            catch (Exception)
            {
                return;//a esconder mi amor vamos a esconder mi amor...
            }
        }

        public void createPersistentDefaultModel()
        {
            createPersistentDefaultModel(false);
        }

        private void loadObjectsAndTypes()
        {
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();

            foreach (var type in types)
            {
                if (typeof(Serializable).IsAssignableFrom(type))
                {
                    if (type != typeof(Serializable))
                    {
                        Serializable objeto = (Serializable)Activator.CreateInstance(type);
                        dictionaryObjectsAndTypes.Add(type, objeto);
                    }
                }
            }
        }

        private void createIncompleteTables()
        {
            foreach (var item in dictionaryObjectsAndTypes.Values)
            {
                createIncompleteTable(item);
            }
        }

        private void createForeignKeys()
        {
            foreach (var item in dictionaryObjectsAndTypes.Values)
            {
                createForeignKeys(item);
            }
        }

        private void createForeignKeys(Serializable objeto)
        {
            int serializablePropertyCounter = listProperties(objeto).Count(property => 
                                                isSerializableProperty(property, objeto));

            if (serializablePropertyCounter==0)
            {
                return;
            }

            string alterQuery = "alter table " + objeto.getTableName()+" add ";

            string foreignKeys = "";

            foreach (var item in listProperties(objeto))
            {
                Type propertyType = getPropertyType(item, objeto);

                if (isSerializableProperty(item,objeto))
                {
                    Serializable property = dictionaryObjectsAndTypes[propertyType];
                    Type idPropertyType = getPropertyType(property.getIdPropertyName(), property);

                    string primaryKeyProperty = property.getMapFromKey(property.getIdPropertyName());
                    string foreignKeyProperty = objeto.getMapFromKey(item);
                    string tableNameProperty = property.getTableName();

                    alterQuery += foreignKeyProperty + " " + getDataTypeName(idPropertyType) + ",";
                    foreignKeys += "foreign key(" + foreignKeyProperty + ") references " 
                                    + tableNameProperty + "(" + primaryKeyProperty + "),";
                }
            }

            foreignKeys = foreignKeys.Remove(foreignKeys.Length - 1);
            alterQuery += foreignKeys;

            DataBase.Instance.ejecutarConsulta(alterQuery);
        }

        private void createIncompleteTable(Serializable objeto)
        {
            string createQuery = "create table " + objeto.getTableName() + "(";

            foreach (var item in listProperties(objeto))
            {
                if (!isSerializableProperty(item,objeto))
                {
                    String dataType = getDataTypeName(getPropertyType(item, objeto));

                    if (dataType!="")
                    {
                        createQuery += objeto.getMapFromKey(item) + " " + dataType;

                        if (objeto.getIdPropertyName()==item)
                        {
                            createQuery += " not null primary key";

                            if (objeto.getPrimaryKeyType()==Serializable.PrimaryKeyType.SURROGATE)
                            {
                                createQuery += " identity(1,1)";
                            }
                        }

                        createQuery += ",";
                    }
                }
            }

            createQuery = createQuery.Remove(createQuery.Length - 1);
            createQuery += ")";

            DataBase.Instance.ejecutarConsulta(createQuery);
        }

        private string getDataTypeName(Type type)
        {
            if (typeof(int)==type)
            {
                return "int";
            }
            if (typeof(double)==type|| typeof(float) == type)
            {
                return "float";
            }
            if (typeof(char) == type)
            {
                return "char";
            }
            if (typeof(string) == type)
            {
                return "varchar(max)";
            }
            if (typeof(long) == type)
            {
                return "bigint";
            }
            if (typeof(bool) == type)
            {
                return "bit";
            }
            if (typeof(DateTime) == type)
            {
                return "datetime";
            }

            return "";
        }

        private Type getPropertyType(string propertyName, object objeto)
        {
            return objeto.GetType().GetProperty(propertyName).PropertyType;
        }

        private bool isSerializableProperty(string propertyName, Serializable objeto)
        {
            return typeof(Serializable).IsAssignableFrom(getPropertyType(propertyName, objeto));
        }

        private List<String> listProperties(object objeto)
        {
            List<String> properties = new List<string>();

            foreach (MemberInfo info in objeto.GetType().GetMembers())
            {
                if (info.MemberType == MemberTypes.Property)
                {
                    properties.Add(((PropertyInfo)info).Name);
                }
            }

            return properties;
        }

        private bool existsTable(String tableName)
        {
            string existsQuery = "if EXISTS(SELECT * FROM sysobjects  WHERE name = '" +
                tableName + "') select 1 as value else select 0 as value";

            return Convert.ToBoolean(DataBase.Instance.ejecutarConsulta(existsQuery)[0]["value"]);
        }
    }
}

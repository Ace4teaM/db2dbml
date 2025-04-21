using db2dbml;
using Npgsql;
using System.Data;
using System.Linq;
using System.Text;
//using System.Runtime.Remoting.Messaging;

internal static class Postgresql
{
    internal static Dictionary<string, GenericDataType> types = new Dictionary<string, GenericDataType>() {
        { "character varying", GenericDataType.varchar },
        { "bigint", GenericDataType.integer },
        { "bigserial", GenericDataType.autoincrement },
        { "bit", GenericDataType.boolean },
        { "bit varying", GenericDataType.bits },
        { "boolean", GenericDataType.boolean },
        { "box", GenericDataType.integer },
        { "bytea", GenericDataType.bytes },
        { "character", GenericDataType.chars },
        { "cidr", GenericDataType.varchar },//IP
        { "circle", GenericDataType.integer },
        { "date", GenericDataType.date },
        { "double precision", GenericDataType.real },
        { "inet", GenericDataType.varchar },//IP
        { "integer", GenericDataType.integer },
        { "interval", GenericDataType.real },//time span
        { "json", GenericDataType.varchar },
        { "jsonb", GenericDataType.bytes },
        { "line", GenericDataType.varchar },
        { "lseg", GenericDataType.varchar },
        { "macaddr", GenericDataType.bytes },//MAC
        { "macaddr8", GenericDataType.varchar },
        { "money", GenericDataType.real },
        { "numeric", GenericDataType.real },
        { "path", GenericDataType.varchar },
        { "polygon", GenericDataType.varchar },
        { "real", GenericDataType.real },
        { "smallint", GenericDataType.integer },
        { "smallserial", GenericDataType.autoincrement },
        { "serial", GenericDataType.autoincrement },
        { "text", GenericDataType.varchar },
        { "time", GenericDataType.time },
        { "timestamp", GenericDataType.timestamp },
        { "tsquery", GenericDataType.varchar },
        { "tsvector", GenericDataType.varchar },
        { "txid_snapshot", GenericDataType.integer },
        { "uuid", GenericDataType.bytes },
        { "xml", GenericDataType.varchar },
        { "oid", GenericDataType.integer }
    };


    public static string ReadTables(string connString)
    {
        StringBuilder content = new StringBuilder();

        using (NpgsqlConnection conn = new NpgsqlConnection(connString))
        {
            conn.Open();

            var tablesNames = new List<(string, string)>();//schema,name
            var primaryKeys = new List<(string, string, string)>();//schema,name,column
            var foreignKeys = new List<(string, string, string, string, string, string)>();//schema,name,column, to schema,name,column

            // obtient le nom des tables
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "select TABLE_SCHEMA, TABLE_NAME from information_schema.tables where TABLE_SCHEMA = 'public' and TABLE_TYPE = 'BASE TABLE'";//TABLE_TYPE = BASE TABLE, VIEW

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tablesNames.Add((reader["TABLE_SCHEMA"].ToString(), reader["TABLE_NAME"].ToString()));
                }
            }

            // liste les clés primaires
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = @"
select col.TABLE_SCHEMA, col.TABLE_NAME, col.COLUMN_NAME, cst.CONSTRAINT_NAME from information_schema.TABLE_CONSTRAINTS cst
inner join information_schema.KEY_COLUMN_USAGE col on (col.TABLE_SCHEMA = cst.TABLE_SCHEMA and col.TABLE_NAME = cst.TABLE_NAME and col.TABLE_CATALOG = cst.TABLE_CATALOG)
where col.TABLE_SCHEMA = 'public' and CONSTRAINT_TYPE = 'PRIMARY KEY'";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    primaryKeys.Add((reader["TABLE_SCHEMA"].ToString(), reader["TABLE_NAME"].ToString(), reader["COLUMN_NAME"].ToString()));
                }
            }

            // liste les clés étrangères
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = @"
select col.TABLE_SCHEMA, col.TABLE_NAME, col.COLUMN_NAME, cst.CONSTRAINT_NAME, pk.TABLE_SCHEMA as T_TABLE_SCHEMA, pk.TABLE_NAME as T_TABLE_NAME, pk.COLUMN_NAME as T_COLUMN_NAME from information_schema.TABLE_CONSTRAINTS cst
inner join information_schema.KEY_COLUMN_USAGE col on (col.TABLE_SCHEMA = cst.TABLE_SCHEMA and col.TABLE_NAME = cst.TABLE_NAME and col.TABLE_CATALOG = cst.TABLE_CATALOG and col.CONSTRAINT_NAME = cst.CONSTRAINT_NAME)
inner join information_schema.REFERENTIAL_CONSTRAINTS ref on (ref.CONSTRAINT_NAME = col.CONSTRAINT_NAME)
inner join information_schema.KEY_COLUMN_USAGE pk on (ref.UNIQUE_CONSTRAINT_NAME = pk.CONSTRAINT_NAME)
where col.TABLE_SCHEMA = 'public' and CONSTRAINT_TYPE = 'FOREIGN KEY'";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    foreignKeys.Add((reader["TABLE_SCHEMA"].ToString(), reader["TABLE_NAME"].ToString(), reader["COLUMN_NAME"].ToString(),
                                     reader["T_TABLE_SCHEMA"].ToString(), reader["T_TABLE_NAME"].ToString(), reader["T_COLUMN_NAME"].ToString()));
                }
            }

            // obtient la définition des tables
            foreach (var tableName in tablesNames)
            {
                using (NpgsqlCommand cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "select COLUMN_NAME, DATA_TYPE from information_schema.COLUMNS where TABLE_SCHEMA = @SchemaName and TABLE_NAME = @TableName order by ORDINAL_POSITION";

                    cmd.Parameters.AddWithValue("@TableName", tableName.Item2);
                    cmd.Parameters["@TableName"].Direction = ParameterDirection.Input;

                    cmd.Parameters.AddWithValue("@SchemaName", tableName.Item1);
                    cmd.Parameters["@SchemaName"].Direction = ParameterDirection.Input;

                    content.Append(String.Format("Table {0}", tableName.Item2));
                    content.Append(" {");
                    content.AppendLine();

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        content.Append("   ");
                        content.Append(reader["COLUMN_NAME"].ToString());
                        content.Append(" ");
                        try
                        {
                            content.Append(types[reader["DATA_TYPE"].ToString()]);
                        }
                        catch
                        {
                            content.Append(GenericDataType.unknown);
                        }

                        if (primaryKeys.Contains((tableName.Item1, tableName.Item2, reader["COLUMN_NAME"].ToString())))
                            content.Append(" [primary key]");

                        content.AppendLine();
                    }

                    content.AppendLine("}");
                    content.AppendLine();
                }
            }

            // obtient les relations des tables
            if (foreignKeys.Count > 0)
            {
                content.AppendLine();
                foreach (var foreignKey in foreignKeys)
                {
                    content.AppendLine(String.Format("Ref: {0} > {1}", foreignKey.Item2, foreignKey.Item5));
                }
            }

            conn.Close();
        }

        var result = content.ToString();

        return result;
    }
}

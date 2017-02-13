using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Perfon.Interfaces.Common;
using Perfon.Interfaces.PerfCounterStorage;

namespace Perfon.Storage.MySql
{
    /// <summary>
    /// Driver for store/restore performance counter values in MySql.
    /// </summary>
    public class PerfCounterMySqlStorage : IPerfomanceCountersStorage
    {
        private string DbConnectionString { get; set; }

        private ConcurrentBag<string> counterNames = new ConcurrentBag<string>();

        private bool databaseStructureChecked = false;


        /// <summary>
        /// Reports about errors and exceptions occured.
        /// </summary>
        public event EventHandler<IPerfonErrorEventArgs> OnError;


        public PerfCounterMySqlStorage(string dbConnectionString)
        {
            DbConnectionString = dbConnectionString;
        }

        /// <summary>
        /// Awaitable.
        /// </summary>
        /// <param name="counters"></param>
        /// <returns></returns>
        public async Task StorePerfCounters(IEnumerable<IPerfCounterInputData> counters, DateTime? nowArg = null, string appId = null)
        {
            try
            {
                var now = DateTime.Now;
                if (nowArg.HasValue)
                {
                    now = nowArg.Value;
                }


                List<short> counterId = new List<short>();

                bool updateNames = false;

                foreach (var counter in counters)
                {
                    if (!counterNames.Contains(counter.Name))
                    {
                        updateNames = true;
                        break;
                    }
                    counterId.Add((short)(Tools.CalculateHash(counter.Name) % (ulong)short.MaxValue));
                }


                using (var conn = new MySqlConnection(DbConnectionString))
                {
                    conn.Open();

                    if (updateNames)
                    {
                        counterId.Clear();

                        if (!databaseStructureChecked)
                        {
                            await CheckDbstructure(conn);
                        }

                        using (var cmd = new MySqlCommand())
                        {
                            cmd.Connection = conn;

                            foreach (var counter in counters)
                            {
                                short id = -1;

                                // Retrieve counters id
                                cmd.CommandText = @"SELECT Id FROM CounterNames WHERE Name='" + counter.Name + "'";
                                cmd.Parameters.Add("counterName", MySqlDbType.VarChar).Value = counter.Name;
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        id = reader.GetInt16(0);
                                    }
                                }
                                cmd.Parameters.Clear();

                                if (id == -1)
                                {
                                    id = (short)(Tools.CalculateHash(counter.Name) % (ulong)short.MaxValue);
                                    cmd.CommandText = @"INSERT INTO CounterNames (Id,Name) VALUES (@id, @counterName)";
                                    cmd.Parameters.Add("id", MySqlDbType.Int16).Value = id;
                                    cmd.Parameters.Add("counterName", MySqlDbType.VarChar).Value = counter.Name;
                                    cmd.ExecuteNonQuery();
                                    cmd.Parameters.Clear();
                                }
                                counterId.Add(id);
                                counterNames.Add(counter.Name);
                            }
                        }
                    }


                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        StringBuilder sCommand = new StringBuilder("INSERT INTO PerfomanceCounterValues (AppId, CounterId, Timestamp, Value) VALUES ");
                        var sb = new StringBuilder();
                        int i = 0;

                        cmd.Parameters.Add("timestamp", MySqlDbType.DateTime).Value = now;
                            
                        foreach (var counter in counters)
                        {
                            sb.Append("(").Append(0).Append(",").Append(counterId[i]).Append(",@timestamp").Append(", @value").Append(i).Append("),");
                            cmd.Parameters.Add("value" + i, MySqlDbType.Float).Value = counter.Value;
                            i++;
                        }
                        sCommand.Append(sb.ToString().TrimEnd(',')).Append(';');

                        cmd.CommandText = sCommand.ToString();
                        cmd.Connection = conn;

                        cmd.CommandType = CommandType.Text;
                        await cmd.ExecuteNonQueryAsync();
                    }
                    
                    //using (var cmd = new MySqlCommand())
                    //{
                    //    cmd.Connection = conn;

                    //    int i = 0;
                    //    foreach (var counter in counters)
                    //    {
                    //        cmd.CommandText = @"INSERT INTO PerfomanceCounterValues (AppId, CounterId, Timestamp, Value) VALUES (0, @id, @timestamp, @value)";
                    //        cmd.Parameters.Add("id", MySqlDbType.Int16).Value = counterId[i];
                    //        cmd.Parameters.Add("timestamp", MySqlDbType.Timestamp).Value = now;
                    //        cmd.Parameters.Add("value", MySqlDbType.Float).Value = counter.Value;
                    //        cmd.ExecuteNonQuery();
                    //        cmd.Parameters.Clear();


                    //        i++;
                    //    }
                    //}

                }

            }
            catch (Exception exc)
            {
                if (OnError != null)
                {
                    OnError(new object(), new PerfonErrorEventArgs(exc.ToString()));
                }
            }

            return;
        }

        

        public async Task<IEnumerable<IPerfCounterValue>> QueryCounterValues(string counterName, DateTime? date = null, int skip = 0, string appId = null)
        {
            var list = new List<IPerfCounterValue>();

            if (!date.HasValue)
            {
                date = DateTime.Now;
            }

            date = date.Value.Date;

            try
            {
                using (var conn = new MySqlConnection(DbConnectionString))
                {
                    conn.Open();

                    if (!databaseStructureChecked)
                    {
                        await CheckDbstructure(conn);
                    }                            

                    using (var cmd = new MySqlCommand())
                    {
                        cmd.Connection = conn;

                        var id = (short)(Tools.CalculateHash(counterName) % (ulong)short.MaxValue);

                        cmd.CommandText = @"SELECT Timestamp,Value FROM PerfomanceCounterValues 
WHERE AppId=0 AND CounterId=@id AND Timestamp >= @timestamp AND CAST(Timestamp AS DATE) = @timestamp ORDER BY Timestamp LIMIT 18446744073709551615 OFFSET @skip";

                        cmd.Parameters.Add("id", MySqlDbType.Int16).Value = id;
                        cmd.Parameters.Add("timestamp", MySqlDbType.Timestamp).Value = date;
                        cmd.Parameters.Add("skip", MySqlDbType.Int16).Value = skip;
                        
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {
                                var timeStamp = new DateTime(reader.GetDateTime(0).Ticks);
                                var value = reader.GetFloat(1);

                                list.Add(new PerfCounterValue(timeStamp, value));
                            }
                        }
                    }

                }
            }
            catch (Exception exc)
            {
                if (OnError != null)
                {
                    OnError(new object(), new PerfonErrorEventArgs(exc.ToString()));
                }
            }

            return list as IEnumerable<IPerfCounterValue>;
        }

        public async Task<IEnumerable<string>> GetCountersList()
        {
            var res = new List<string>();

            try
            {
                using (var conn = new MySqlConnection(DbConnectionString))
                {
                    conn.Open();

                    if (!databaseStructureChecked)
                    {
                        await CheckDbstructure(conn);
                    }

                    using (var cmd = new MySqlCommand())
                    {
                        cmd.Connection = conn;

                        cmd.CommandText = @"SELECT Name FROM CounterNames ";
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {
                                res.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                if (OnError != null)
                {
                    OnError(new object(), new PerfonErrorEventArgs(exc.ToString()));
                }
            }

            return res as IEnumerable<string>;
        }


        private string sqlStructureText = "";

        private async Task CheckDbstructure(MySqlConnection conn)
        {
            if (string.IsNullOrEmpty(sqlStructureText))
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Perfon.Storage.MySql.db_structure.sql";

                try
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            sqlStructureText = reader.ReadToEnd();
                        }
                    }
                }
                catch (Exception exc)
                {
                    if (OnError != null)
                    {
                        OnError(this, new PerfonErrorEventArgs(exc.ToString()));
                    }
                }
            }

            using (var cmd = new MySqlCommand())
            {
                cmd.Connection = conn;

                cmd.CommandText = sqlStructureText;
                await cmd.ExecuteNonQueryAsync();
            }
            databaseStructureChecked = true;
        }

    }
}

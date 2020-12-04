using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core.Flux.Domain;
using InfluxDB.Client.Writes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using XMIoT.Framework.Settings.Enums;

namespace XMPro.InfluxDB
{
    public class InfluxDbService
    {
        public InfluxDBClient Client { get; set; }
        public string Bucket { get; set; }
        public string Org { get; set; }
        public int ReadWriteTimeout { get; set; }
        public int SocketTimeout { get; set; }
        public bool EnableGZip
        {
            set
            {
                if (Client != null)
                {
                    if (value)
                    {
                        Client.EnableGzip();
                    }
                    else
                    {
                        if (Client.IsGzipEnabled())
                        {
                            Client.DisableGzip();
                        }
                    }
                }
            }
        }


        public InfluxDbService(string url, string token, string org)
        {
            Client = InfluxDBClientFactory.Create(url, token.ToCharArray());
            Org = org;
        }

        public InfluxDbService(string url, string token, string bucket, string org, int readWriteTimeout = 60, int socketTimeout = 60, bool enableGzip= true)
        {
            Client = InfluxDBClientFactory.Create(url, token.ToCharArray());

            if (enableGzip)
            {
                Client.EnableGzip();
            }

            Bucket = bucket;
            Org = org;
            ReadWriteTimeout = readWriteTimeout;
            SocketTimeout = socketTimeout;
        }

        public async Task<List<Bucket>> GetBucketsList()
        {
            var bucketsApi = Client.GetBucketsApi();
            return await bucketsApi.FindBucketsByOrgNameAsync(Org);
        }

        public void WriteLinearMeasurement(string bucket, string org, string measurememnt)
        {
            using (var writeApi = Client.GetWriteApi())
            {
                writeApi.WriteRecord(bucket, org, WritePrecision.Ns, measurememnt);
            }
        }

        public string WritePoint(PointData point)
        {
            string result = string.Empty;
            try
            {
                using (var writeApi = Client.GetWriteApi())
                {
                    writeApi.WritePoint(Bucket, Org, point);
                }
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return result;
        }

        public List<FluxTable> GetQueryResult(string query)
        {
            return Client.GetQueryApi().QueryAsync(query, Org).Result;
        }
    }
}

using System;
using System.Data;
using System.Linq;
using System.Data.SqlClient;
using XMIoT.Framework;
using System.Collections;
using System.Collections.Generic;
using XMIoT.Framework.Settings;
using XMIoT.Framework.Helpers;
using InfluxDB.Client.Api.Domain;
using XMIoT.Framework.Settings.Enums;
using InfluxDB.Client.Core.Flux.Domain;

namespace XMPro.InfluxDB
{
    public class ContextProvider
    {
        private Configuration config;
        private List<XMIoT.Framework.Attribute> parentOutputs;
        private Grid _tagMappings;
        private Grid _fieldMappings;

        public const string InputEndpoint = "Input";
        public const string OutputEndpoint = "Output";

        private const string URLConst = "URL";
        private const string TokenConst = "Token";
        private const string BucketConst = "Bucket";
        private const string OrgConst = "Org";
        private const string OutputConst = "Output";
        private const string UseVariablesConst = "UseVariables";
        private const string ResultInfluxDbColumnNameConst = "result";
        private const string TableInfluxDbColumnNameConst = "table";
        private const string StartInfluxDbColumnNameConst = "_start";
        private const string StopInfluxDbColumnNameConst = "_stop";
        private const string TimeInfluxDbColumnNameConst = "_time";
        private const string ValueInfluxDbColumnNameConst = "_value";
        private const string FieldInfluxDbColumnNameConst = "_field";
        private const string MeasurementsInfluxDbColumnNameConst = "_measurement";
        private const string MeasurementTitleConst = "Measurement";
        private const string MeasurementTagValueConst = "TagValue";
        private const string MeasurementFieldValueConst = "FieldKey";
        private const string TagMappingsConst = "TagMappings";
        private const string TagMappingColumnNameConst = "TagName";
        private const string TagMappingColumnValueConst = "TagValue";
        private const string TagKeyName = "Host";
        private const string FieldMappingsConst = "FieldMappings";
        private const string FieldMappingColumnTypeConst = "FieldType";
        private const string FieldMappingColumnNameConst = "FieldName";

        private bool UseVariables => bool.TryParse(this.config[UseVariablesConst], out bool result) && result;
        private string URL => UseVariables ? GetVariableValue(this.config["v" + URLConst]) : this.config[URLConst];
        private string Token => UseVariables ? GetVariableValue(this.config["v" + TokenConst], true) : Decrypt(this.config[TokenConst]);
        private string Bucket => this.config[BucketConst];
        private string Org => UseVariables ? GetVariableValue(this.config["v" + OrgConst]) : this.config[OrgConst];

        private string MeasurementTitle => this.config[MeasurementTitleConst];
        private string MeasurementTagValue => this.config[MeasurementTagValueConst];
        private string MeasurementFieldValue => this.config[MeasurementFieldValueConst];

        private string Output => this.config[OutputConst];

        public long UniqueId { get; set; }

        public InfluxDbService InfluxDbService { get; set; }

        public event EventHandler<OnPublishArgs> OnPublish;

        public event EventHandler<OnDecryptRequestArgs> OnDecryptRequest;
        public event EventHandler<OnVariableRequestArgs> OnVariableRequest;

        public Grid TagMapping
        {
            get
            {
                if (_tagMappings == null)
                {
                    _tagMappings = new Grid();
                    _tagMappings.Value = this.config[TagMappingsConst];
                }
                return _tagMappings;
            }
        }

        public Grid FieldMapping
        {
            get
            {
                if (_fieldMappings == null)
                {
                    _fieldMappings = new Grid();
                    _fieldMappings.Value = this.config[FieldMappingsConst];
                }
                return _fieldMappings;
            }
        }

        public string GetConfigurationTemplate(string template, IDictionary<string, string> parameters)
        {
            Settings settings = Settings.Parse(template);
            new Populator(parameters).Populate(settings);

            CheckBox useVariables = settings.Find(UseVariablesConst) as CheckBox;
            TextBox URLTextBox = settings.Find(URLConst) as TextBox;
            TextBox tokenTextBox = settings.Find(TokenConst) as TextBox;
            TextBox orgTextBox = settings.Find(OrgConst) as TextBox;
            VariableBox URLVarBox = settings.Find("v" + URLConst) as VariableBox;
            VariableBox tokenVarBox = settings.Find("v" + TokenConst) as VariableBox;
            VariableBox orgVarBox = settings.Find("v" + OrgConst) as VariableBox;


            URLTextBox.Required = tokenTextBox.Required = orgTextBox.Required = URLTextBox.Visible = tokenTextBox.Visible = orgTextBox.Visible = useVariables.Value != true;
            URLVarBox.Required = tokenVarBox.Required = orgVarBox.Required = URLVarBox.Visible = tokenVarBox.Visible = orgVarBox.Visible = useVariables.Value == true;

            string url;
            string token;
            string org;

            if (useVariables.Value)
            {
                url = GetVariableValue(orgVarBox.Value);
                token = GetVariableValue(tokenVarBox.Value);
                org = GetVariableValue(orgVarBox.Value);
            }
            else
            {
                url = URLTextBox.Value;
                token = tokenTextBox.Value;
                org = orgTextBox.Value;
            }

            if (!StringExtentions.IsNullOrWhiteSpace(url, token, org))
            {
                InfluxDbService = new InfluxDbService(url, Decrypt(token), orgTextBox.Value);
            }
            if (InfluxDbService != null)
            {
                DropDown bucketsDropDown = settings.Find(BucketConst) as DropDown;
                List<Bucket> bucketsList = this.InfluxDbService.GetBucketsList().Result;
                if (bucketsDropDown != null && bucketsList != null)
                {
                    bucketsDropDown.Options = bucketsList.Select((bucket => new Option() { DisplayMemeber = bucket.Name, ValueMemeber = bucket.Name })).ToList();
                }
                if (!string.IsNullOrEmpty(bucketsDropDown.Value))
                {
                    InfluxDbService.Bucket = bucketsDropDown.Value;
                }
            }

            return settings.ToString();
        }

        public IEnumerable<XMIoT.Framework.Attribute> GetOutputAttributes(string endpoint, IDictionary<string, string> parameters)
        {
            this.config = new Configuration() { Parameters = parameters };

            List<XMIoT.Framework.Attribute> outputs = new List<XMIoT.Framework.Attribute>();

            outputs.Add(new XMIoT.Framework.Attribute(ResultInfluxDbColumnNameConst, XMIoT.Framework.Settings.Enums.Types.String));
            outputs.Add(new XMIoT.Framework.Attribute(TableInfluxDbColumnNameConst, XMIoT.Framework.Settings.Enums.Types.String));
            outputs.Add(new XMIoT.Framework.Attribute(StartInfluxDbColumnNameConst, XMIoT.Framework.Settings.Enums.Types.String));
            outputs.Add(new XMIoT.Framework.Attribute(StopInfluxDbColumnNameConst, XMIoT.Framework.Settings.Enums.Types.String));
            outputs.Add(new XMIoT.Framework.Attribute(TimeInfluxDbColumnNameConst, XMIoT.Framework.Settings.Enums.Types.String));
            outputs.Add(new XMIoT.Framework.Attribute(ValueInfluxDbColumnNameConst, XMIoT.Framework.Settings.Enums.Types.String));
            outputs.Add(new XMIoT.Framework.Attribute(FieldInfluxDbColumnNameConst, XMIoT.Framework.Settings.Enums.Types.String));
            outputs.Add(new XMIoT.Framework.Attribute(MeasurementsInfluxDbColumnNameConst, XMIoT.Framework.Settings.Enums.Types.String));
            foreach (var row in TagMapping.Rows)
            {
                string tagName = row[TagMappingColumnNameConst].ToString();
                outputs.Add(new XMIoT.Framework.Attribute(tagName, Types.String));
            }
            return outputs;
        }

        public void Create(Configuration configuration)
        {
            this.config = configuration;
            if (InfluxDbService == null)
            {
                this.InfluxDbService = new InfluxDbService(URL, Token, Bucket, Org);
            }
        }

        public string CreateQuery(string start = null)
        {
            string query = $"from(bucket: \"{Bucket}\")\n";
            if (start != null)
                query += $"|> range(start: {start})\n";
            query += $"|> filter(fn: (r) => r[\"{MeasurementsInfluxDbColumnNameConst}\"] == \"{MeasurementTitle}\")\n"; //measurement
            query += $"|> filter(fn: (r) => r[\"{FieldInfluxDbColumnNameConst}\"] == \"{MeasurementFieldValue}\")\n"; //field

            //add each tag
            foreach (var row in TagMapping.Rows)
            {
                string tagName = row[TagMappingColumnNameConst].ToString();
                string tagValue = row[TagMappingColumnValueConst].ToString();
                query += $"|> filter(fn: (r) => r[\"{tagName}\"] == \"{tagValue}\")\n";
            }
            return query;
        }

        public void Poll()
        {
            string query = "";// CreateQuery(_lastTimestamp.AddSeconds(1).ToUniversalTime().ToString("s") + "Z");
            List<FluxTable> tables = InfluxDbService.GetQueryResult(query);
            IList<IDictionary<string, object>> results = new List<IDictionary<string, object>>();
            DateTime currentLastTimestamp = DateTime.MinValue;
            foreach (var table in tables)
            {
                foreach (var record in table.Records)
                {
                    currentLastTimestamp = Convert.ToDateTime(record.GetValueByKey(TimeInfluxDbColumnNameConst).ToString());
                    IDictionary<string, object> keyValuePairs = new Dictionary<string, object>();
                    keyValuePairs.Add(ResultInfluxDbColumnNameConst, record.GetValueByKey(ResultInfluxDbColumnNameConst));
                    keyValuePairs.Add(TableInfluxDbColumnNameConst, record.GetValueByKey(TableInfluxDbColumnNameConst));
                    keyValuePairs.Add(StartInfluxDbColumnNameConst, record.GetValueByKey(StartInfluxDbColumnNameConst).ToString());
                    keyValuePairs.Add(StopInfluxDbColumnNameConst, record.GetValueByKey(StopInfluxDbColumnNameConst).ToString());
                    keyValuePairs.Add(TimeInfluxDbColumnNameConst, record.GetValueByKey(TimeInfluxDbColumnNameConst).ToString());
                    keyValuePairs.Add(ValueInfluxDbColumnNameConst, record.GetValueByKey(ValueInfluxDbColumnNameConst));
                    foreach (var row in TagMapping.Rows)
                    {
                        string tagName = row[TagMappingColumnNameConst].ToString();
                        string tagValue = row[TagMappingColumnValueConst].ToString();
                        keyValuePairs.Add(tagName, tagValue);
                    }
                    keyValuePairs.Add(TagKeyName, record.GetValueByKey(TagKeyName));
                    foreach (var row in TagMapping.Rows)
                    {
                        string fieldName = row[FieldInfluxDbColumnNameConst].ToString();
                        keyValuePairs.Add(fieldName, record.GetValueByKey(fieldName));

                    }
                    keyValuePairs.Add(MeasurementsInfluxDbColumnNameConst, record.GetValueByKey(MeasurementsInfluxDbColumnNameConst));
                    results.Add(keyValuePairs);
                }
            }

            if (results.Count > 0)
                this.OnPublish?.Invoke(this, new OnPublishArgs(results.ToArray(), OutputConst));

        }

        private string GetVariableValue(string variableName, bool isEncrypted = false)
        {
            var x = new OnVariableRequestArgs(variableName);
            this.OnVariableRequest?.Invoke(this, x);
            return isEncrypted ? this.Decrypt(x.Value) : x.Value;
        }

        private string Decrypt(string value)
        {
            var request = new OnDecryptRequestArgs(value);
            this.OnDecryptRequest?.Invoke(this, request);
            return request.DecryptedValue;
        }

        public void Destroy()
        {
        }

        public string[] Validate(IDictionary<string, string> parameters)
        {
            int i = 1;
            List<string> errors = new List<string>();
            this.config = new Configuration() { Parameters = parameters };

            if (string.IsNullOrWhiteSpace(this.URL))
                errors.Add($"Error {i++}: Url is not specified.");
            if (string.IsNullOrWhiteSpace(this.Token))
                errors.Add($"Error {i++}: Token is not specified.");
            if (string.IsNullOrWhiteSpace(this.Org))
                errors.Add($"Error {i++}: Org is not specified.");
            if (string.IsNullOrWhiteSpace(this.MeasurementTitle))
                errors.Add($"Error {i++}: Measurement is not specified.");
            if (string.IsNullOrWhiteSpace(this.FieldMapping.Value))
                errors.Add($"Error {i++}: No Fields not specified.");

            return errors.ToArray();
        }
    }
}

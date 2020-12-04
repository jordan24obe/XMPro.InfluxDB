using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XMIoT.Framework;
using XMIoT.Framework.Settings;
using XMIoT.Framework.Settings.Enums;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace XMPro.InfluxDB
{
    public class InfluxDbActionAgent : IAgent, IMapAndReceiveAgent, IUsesVariable, IPublishesError
    {
        private Configuration config;
        private List<XMIoT.Framework.Attribute> parentOutputs;
        private Dictionary<string, Types> _inputParameters;
        private Grid _fieldMappings;
        private Grid _tagMappings;

        public const string InputEndpoint = "Input";
        public const string OutputEndpoint = "Output";
        private const string MeasurementTitleInputConst = "MeasurementTitle";
        private const string URLConst = "URL";
        private const string TokenConst = "Token";
        private const string BucketConst = "Bucket";
        private const string OrgConst = "Org";
        private const string ReadWriteTimeoutConst = "ReadWriteTimeout";
        private const string SocketTimeoutConst = "SocketTimeout";
        private const string EnableGZipConst = "EnableGZip";
        private const string UseVariablesConst = "UseVariables";
        private const string FieldMappingsConst = "FieldMappings";
        private const string FieldMappingColumnKeyConst = "FieldKey";
        private const string FieldMappingColumnNameConst = "FieldName";
        private const string TagMappingsConst = "TagMappings";
        private const string TagMappingColumnKeyConst = "TagName";
        private const string TagMappingColumnNameConst = "TagValue";

        private bool UseVariables => bool.TryParse(this.config[UseVariablesConst], out bool result) && result;
        private string URL => UseVariables ? GetVariableValue(this.config["v" + URLConst]) : this.config[URLConst];
        private string Token => UseVariables ? GetVariableValue(this.config["v" + TokenConst], true) : Decrypt(this.config[TokenConst]);
        private string Bucket => this.config[BucketConst];
        private string Org => UseVariables ? GetVariableValue(this.config["v" + OrgConst]) : this.config[OrgConst];
        private int ReadWriteTimeout => Convert.ToInt32(this.config[ReadWriteTimeoutConst]);
        private int SocketTimeout => Convert.ToInt32(this.config[SocketTimeoutConst]);
        private string EnableGZip => this.config[EnableGZipConst];

        private List<XMIoT.Framework.Attribute> ParentOutputs
        {
            get
            {
                if (parentOutputs == null)
                {
                    var args = new OnRequestParentOutputAttributesArgs(this.UniqueId, InputEndpoint);
                    this.OnRequestParentOutputAttributes.Invoke(this, args);
                    parentOutputs = args.ParentOutputs.ToList();
                }
                return parentOutputs;
            }
        }

        public long UniqueId { get; set; }

        public InfluxDbService InfluxDbService { get; set; }
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

        public event EventHandler<OnPublishArgs> OnPublish;
        public event EventHandler<OnRequestParentOutputAttributesArgs> OnRequestParentOutputAttributes;
        public event EventHandler<OnDecryptRequestArgs> OnDecryptRequest;
        public event EventHandler<OnVariableRequestArgs> OnVariableRequest;
        public event EventHandler<OnErrorArgs> OnPublishError;

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


            if (!XMIoT.Framework.Helpers.StringExtentions.IsNullOrWhiteSpace(url, token, org))
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
                TextBox readTimeoutTextBox = settings.Find(ReadWriteTimeoutConst) as TextBox;
                TextBox socketTimeoutTextBox = settings.Find(SocketTimeoutConst) as TextBox;
                TextBox enableGzipTextBox = settings.Find(EnableGZipConst) as TextBox;

                Grid list = settings.Find(FieldMappingsConst) as Grid;
                var fieldsDropdown = list.Columns.Find(c => c.Key == FieldMappingColumnKeyConst) as DropDown;
                fieldsDropdown.Options = ParentOutputs.Select(o => new Option() { DisplayMemeber = o.Name, ValueMemeber = o.Name }).ToList();

                Grid tags = settings.Find(TagMappingsConst) as Grid;
                var tagsDropdown = tags.Columns.Find(c => c.Key == TagMappingColumnKeyConst) as DropDown;
                tagsDropdown.Options = ParentOutputs.Select(o => new Option() { DisplayMemeber = o.Name, ValueMemeber = o.Name }).ToList();

                if (enableGzipTextBox != null && !string.IsNullOrWhiteSpace(enableGzipTextBox.Value))
                {
                    InfluxDbService.EnableGZip = Convert.ToBoolean(enableGzipTextBox);
                }
            }

            return settings.ToString();
        }
        public IEnumerable<XMIoT.Framework.Attribute> GetInputAttributes(string endpoint, IDictionary<string, string> parameters)
        {
            List<XMIoT.Framework.Attribute> inputs = new List<XMIoT.Framework.Attribute>();
            inputs.Add(new XMIoT.Framework.Attribute(MeasurementTitleInputConst, Types.String));
            return inputs;
        }

        public IEnumerable<XMIoT.Framework.Attribute> GetOutputAttributes(string endpoint, IDictionary<string, string> parameters)
        {
            this.config = new Configuration() { Parameters = parameters };
            var outputs = ParentOutputs;
            return outputs;
        }

        public void Create(Configuration configuration)
        {
            this.config = configuration;
            if (InfluxDbService == null)
            {
                this.InfluxDbService = new InfluxDbService(URL, Token, Bucket, Org, ReadWriteTimeout, SocketTimeout, Convert.ToBoolean(EnableGZip));
            }
            this._inputParameters = new Dictionary<string, Types>(); //map the inputParameters to a dictionary to quickly retrieve them as needed at runtime.
            ParentOutputs.ForEach(i => _inputParameters.Add(i.Name, i.Type));
        }

        public void Start()
        {
        }

        public void Receive(string endpointName, JArray events, JArray mappedEvents)
        {
            try
            {
                for (int i = 0; i < events.Count; i++)
                {
                    JObject _event = events[i] as JObject;
                    JObject mapped = mappedEvents[i] as JObject;
                    string measurementTitle = mapped[MeasurementTitleInputConst].ToString();
                    PointData data = PointData.Measurement(measurementTitle)
                                              .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

                    foreach (var row in TagMapping.Rows)
                    {
                        string tagName = row[TagMappingColumnNameConst].ToString();
                        string tagValue = _event[row[TagMappingColumnKeyConst].ToString()].ToString();
                        data = data.Tag(tagName, tagValue);
                    }
                    foreach (var row in FieldMapping.Rows)
                    {
                        string fieldName = row[FieldMappingColumnNameConst].ToString();
                        string fieldValue = _event[row[FieldMappingColumnKeyConst].ToString()].ToString();
                        data = AddField(data, fieldName, fieldValue, _inputParameters[row[FieldMappingColumnKeyConst].ToString()]);
                    }
                    InfluxDbService.WritePoint(data);
                }

                this.OnPublish(this, new OnPublishArgs(events, OutputEndpoint));
            }
            catch(Exception ex)
            {
                this.OnPublishError?.Invoke(this, new OnErrorArgs(this.UniqueId, DateTime.UtcNow, nameof(Receive), ex.Message, ex.InnerException?.Message ?? ""));
            }
        }

        public PointData AddField(PointData point, string fieldName, string fieldValue, Types fieldType)
        {
            return fieldType switch
            {
                Types.Long => point.Field(fieldName, Convert.ToInt64(fieldValue)),
                Types.Double => point.Field(fieldName, Convert.ToDouble(fieldValue)),
                Types.Boolean => point.Field(fieldName, Convert.ToBoolean(fieldValue)),
                Types.Int => point.Field(fieldName, Convert.ToInt32(fieldValue)),
                _ => point.Field(fieldName, fieldValue),
            };
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
            if (string.IsNullOrWhiteSpace(this.Bucket))
                errors.Add($"Error {i++}: Bucket is not specified.");

            return errors.ToArray();
        }

        public string GetVariableValue(string variableName, bool isEncrypted = false)
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

        public void Receive(string endpointName, JArray events) => throw new NotImplementedException();
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnifiedAutomation.UaBase;
using UnifiedAutomation.UaServer;



namespace OPC_UA_Server
{
    class Program
    {
        private const string DEFAULT_XML_PATH = "config.xml";
        private static Server _server;

        public static object DataTypes { get; private set; }

        static void Main(string[] args)
        {
            try
            {
                string xmlPath = args.Length > 0 ? args[0] : DEFAULT_XML_PATH;

                // Initialize server
                _server = new Server();
                _server.Start();

                var config = LoadConfigFromXml(xmlPath);
                CreateOpcUaStructure(config);

                Console.WriteLine($"Server running with {config.Nodes.Count} ");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                _server?.Stop();
            }
        }

        private static OpcUaConfig LoadConfigFromXml(string filePath)
        {
            var doc = XDocument.Load(filePath);
            return new OpcUaConfig
            {
                RootNode = new NodeConfig
                {
                    Name = doc.Root.Element("Root")?.Attribute("name")?.Value ?? "Root",
                    Nodes = doc.Root.Element("Root")?.Elements("Node")
                        .Select(n => new NodeConfig
                        {
                            Name = n.Attribute("name")?.Value ?? throw new Exception("Missing node name"),
                            Value = n.Attribute("value")?.Value,
                            DataType = ParseDataType(n.Attribute("type")?.Value ?? "double"),
                            Description = n.Attribute("description")?.Value
                        }).ToList() ?? new List<NodeConfig>()
                }
            };
        }

        private static DataTypeId ParseDataType(string typeStr)
        {
            return typeStr.ToLower() switch
            {
                "bool" => DataTypes.Boolean,
                "int" => DataTypes.Int32,
                "double" => DataTypes.Double,
                "float" => DataTypes.Float,
                "string" => DataTypes.String,
                _ => throw new ArgumentException($"Unsupported data type: {typeStr}")
            };
        }

        private static void CreateOpcUaStructure(OpcUaConfig config)
        {
            var rootFolder = new FolderNode(config.RootNode.Name, config.RootNode.Description);
            _server.AddressSpace.AddNode(rootFolder);

            foreach (var nodeConfig in config.RootNode.Nodes)
            {
                var variable = new VariableNode(
                    nodeConfig.Name,
                    ConvertValue(nodeConfig.Value, nodeConfig.DataType),
                    nodeConfig.DataType)
                {
                    Description = nodeConfig.Description
                };
                rootFolder.AddChild(variable);
            }
        }

        private static object ConvertValue(string value, DataTypeId dataType)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            try
            {
                return dataType switch
                {
                    DataTypeId when dataType == DataTypes.Boolean => bool.Parse(value),
                    DataTypeId when dataType == DataTypes.Int32 => int.Parse(value),
                    DataTypeId when dataType == DataTypes.Double => double.Parse(value),
                    DataTypeId when dataType == DataTypes.Float => float.Parse(value),
                    _ => value // Default to string
                };
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Failed to parse value '{value}' as {dataType}", ex);
            }
        }
    }

    public class OpcUaConfig
    {
        public NodeConfig RootNode { get; set; }
    }

    public class NodeConfig
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Value { get; set; }
        public DataTypeId DataType { get; set; }
        public List<NodeConfig> Nodes { get; set; } = new List<NodeConfig>();
    }
}


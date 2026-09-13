using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  Curb Delivery MCP Connector (inline)                                       ║
// ║                                                                            ║
// ║  2 tools with static mock data — no external server needed.                ║
// ║  Based on Power MCP Template v2.1 by Troy Taylor.                          ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

public class Script : ScriptBase
{


    private static readonly McpServerOptions Options = new McpServerOptions
    {
        ServerInfo = new McpServerInfo
        {
            Name = "blastbox-curb-delivery",
            Version = "1.0.0",
            Title = "BlastBox Curb Delivery",
            Description = "BlastBox curbside pickup tools for resolving store status and retrieving current bay availability and queue estimates."
        },
        ProtocolVersion = "2025-11-25",
        Capabilities = new McpCapabilities { Tools = true },
        Instructions = "This connector supplies store and curbside pickup bay facts only. Use get_store_location for canonical store metadata, published hours, and current open status. Treat store hours and open status as informational facts. Call list_bays once for a fresh planning snapshot. Each bay has a fixed orders_ahead value from 6 through 10 and a queue rate of 30 seconds per order. Preserve queue_observed_at for deterministic same-session queue progress. The agent applies weather, queue, and any equal-wait membership tie-break when assigning a bay."
    };

    public override async Task<HttpResponseMessage> ExecuteAsync()
    {
        var handler = new McpRequestHandler(Options);
        RegisterCapabilities(handler);

        var body = await this.Context.Request.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = await handler.HandleAsync(body, this.CancellationToken).ConfigureAwait(false);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(result, Encoding.UTF8, "application/json")
        };
    }

    // ── Static Data ─────────────────────────────────────────────────────

    private static readonly JArray Stores = JArray.Parse(@"[
  {
    ""location"": ""Springfield, IL"",
    ""name"": ""BlastBox Springfield"",
    ""address"": ""742 Evergreen Terrace, Springfield, IL 62704"",
    ""lat"": 39.7817,
    ""lon"": -89.6501,
    ""timezone_id"": ""America/Chicago"",
    ""bays"": [
      { ""bay_id"": 1, ""covered"": true,  ""lit"": true,  ""facing"": 90,  ""wind_protection"": ""high"",   ""drainage"": ""good"", ""distance_to_entrance_m"": 12, ""priority"": true,        ""orders_ahead"": 7 },
      { ""bay_id"": 2, ""covered"": true,  ""lit"": true,  ""facing"": 180, ""wind_protection"": ""medium"", ""drainage"": ""good"", ""distance_to_entrance_m"": 20, ""priority"": false,       ""orders_ahead"": 6 },
      { ""bay_id"": 3, ""covered"": false, ""lit"": false, ""facing"": 270, ""wind_protection"": ""low"",    ""drainage"": ""fair"", ""distance_to_entrance_m"": 8,  ""priority"": false,       ""orders_ahead"": 8 }
    ],
    ""occupancy"": { ""1"": null, ""2"": null, ""3"": null }
  },
  {
    ""location"": ""Los Angeles, CA"",
    ""name"": ""BlastBox Los Angeles"",
    ""address"": ""88 Sunset Blvd, Apt 4B, Los Angeles, CA 90028"",
    ""lat"": 34.0522,
    ""lon"": -118.2437,
    ""timezone_id"": ""America/Los_Angeles"",
    ""bays"": [
      { ""bay_id"": 1, ""covered"": false, ""lit"": true,  ""facing"": 0,   ""wind_protection"": ""high"",   ""drainage"": ""good"", ""distance_to_entrance_m"": 10, ""priority"": true,        ""orders_ahead"": 6 },
      { ""bay_id"": 2, ""covered"": false, ""lit"": true,  ""facing"": 45,  ""wind_protection"": ""medium"", ""drainage"": ""fair"", ""distance_to_entrance_m"": 14, ""priority"": false,       ""orders_ahead"": 7 },
      { ""bay_id"": 3, ""covered"": false, ""lit"": true,  ""facing"": 90,  ""wind_protection"": ""low"",    ""drainage"": ""good"", ""distance_to_entrance_m"": 18, ""priority"": false,       ""orders_ahead"": 8 },
      { ""bay_id"": 4, ""covered"": false, ""lit"": false, ""facing"": 180, ""wind_protection"": ""high"",   ""drainage"": ""poor"", ""distance_to_entrance_m"": 8,  ""priority"": false,       ""orders_ahead"": 9 },
      { ""bay_id"": 5, ""covered"": false, ""lit"": false, ""facing"": 270, ""wind_protection"": ""medium"", ""drainage"": ""fair"", ""distance_to_entrance_m"": 22, ""priority"": false,       ""orders_ahead"": 10 },
      { ""bay_id"": 6, ""covered"": false, ""lit"": false, ""facing"": 315, ""wind_protection"": ""high"",   ""drainage"": ""good"", ""distance_to_entrance_m"": 28, ""priority"": false,       ""orders_ahead"": 7 }
    ],
    ""occupancy"": { ""1"": null, ""2"": null, ""3"": null, ""4"": null, ""5"": null, ""6"": null }
  },
  {
    ""location"": ""Seattle, WA"",
    ""name"": ""BlastBox Seattle"",
    ""address"": ""1200 Main Street, Unit 7C, Seattle, WA 98101"",
    ""lat"": 47.61,
    ""lon"": -122.33,
    ""timezone_id"": ""America/Los_Angeles"",
    ""bays"": [
      { ""bay_id"": 1, ""covered"": false, ""lit"": true,  ""facing"": 0,   ""wind_protection"": ""medium"", ""drainage"": ""good"", ""distance_to_entrance_m"": 10, ""priority"": false,       ""orders_ahead"": 7 },
      { ""bay_id"": 2, ""covered"": false, ""lit"": true,  ""facing"": 90,  ""wind_protection"": ""high"",   ""drainage"": ""fair"", ""distance_to_entrance_m"": 16, ""priority"": false,       ""orders_ahead"": 8 },
      { ""bay_id"": 3, ""covered"": true,  ""lit"": true,  ""facing"": 180, ""wind_protection"": ""high"",   ""drainage"": ""good"", ""distance_to_entrance_m"": 22, ""priority"": true,        ""orders_ahead"": 6 },
      { ""bay_id"": 4, ""covered"": false, ""lit"": false, ""facing"": 270, ""wind_protection"": ""low"",    ""drainage"": ""poor"", ""distance_to_entrance_m"": 8,  ""priority"": false,       ""orders_ahead"": 9 }
    ],
    ""occupancy"": { ""1"": null, ""2"": null, ""3"": ""ORD-OTHER"", ""4"": null }
  },
  {
    ""location"": ""Pixel Heights, WA"",
    ""name"": ""BlastBox Pixel Heights"",
    ""address"": ""15 Arcade Lane, Pixel Heights, WA 98052"",
    ""lat"": 47.6740,
    ""lon"": -122.1215,
    ""timezone_id"": ""America/Los_Angeles"",
    ""bays"": [
      { ""bay_id"": 1, ""covered"": false, ""lit"": true,  ""facing"": 90,  ""wind_protection"": ""medium"", ""drainage"": ""good"", ""distance_to_entrance_m"": 12, ""priority"": false,       ""orders_ahead"": 8 },
      { ""bay_id"": 2, ""covered"": true,  ""lit"": true,  ""facing"": 180, ""wind_protection"": ""high"",   ""drainage"": ""good"", ""distance_to_entrance_m"": 20, ""priority"": true,        ""orders_ahead"": 6 },
      { ""bay_id"": 3, ""covered"": false, ""lit"": true,  ""facing"": 270, ""wind_protection"": ""medium"", ""drainage"": ""fair"", ""distance_to_entrance_m"": 8,  ""priority"": false,       ""orders_ahead"": 7 },
      { ""bay_id"": 4, ""covered"": false, ""lit"": false, ""facing"": 0,   ""wind_protection"": ""low"",    ""drainage"": ""poor"", ""distance_to_entrance_m"": 26, ""priority"": false,       ""orders_ahead"": 9 }
    ],
    ""occupancy"": { ""1"": null, ""2"": null, ""3"": null, ""4"": null }
  }
]");

    private static readonly JObject OpeningHours = JObject.Parse(@"{
  ""monday"": ""09:00-18:00"",
  ""tuesday"": ""09:00-18:00"",
  ""wednesday"": ""09:00-18:00"",
  ""thursday"": ""09:00-18:00"",
  ""friday"": ""09:00-18:00"",
  ""saturday"": ""09:00-21:00"",
  ""sunday"": ""09:00-21:00""
}");

    // ── Fixture Helpers ─────────────────────────────────────────────────

    private static string NormalizeLocation(string location)
    {
        return new string((location ?? "")
            .ToLowerInvariant()
            .Replace("illinois", "il")
            .Replace("california", "ca")
            .Replace("washington", "wa")
            .Replace("blastbox", "")
            .Replace("store", "")
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static JObject FindStore(string location)
    {
        var normalized = NormalizeLocation(location);
        foreach (var token in Stores)
        {
            var storeLocation = NormalizeLocation(token["location"]?.ToString());
            if (normalized == storeLocation || normalized.Contains(storeLocation))
                return token as JObject;
        }
        return null;
    }

    private static JArray KnownStores()
    {
        var locations = Stores
            .Select(store => store["location"]?.ToString())
            .Where(location => !string.IsNullOrEmpty(location))
            .OrderBy(location => location, StringComparer.Ordinal)
            .ToArray();
        return new JArray(locations);
    }

    private static JObject UnknownStore()
    {
        return new JObject
        {
            ["error"] = "Unknown store",
            ["known_locations"] = KnownStores()
        };
    }

    private static TimeZoneInfo ResolveStoreTimeZone(string timezoneId)
    {
        string[] candidates;
        if (timezoneId == "America/Chicago")
            candidates = new[] { "America/Chicago", "Central Standard Time" };
        else if (timezoneId == "America/Los_Angeles")
            candidates = new[] { "America/Los_Angeles", "Pacific Standard Time" };
        else
            throw new InvalidOperationException($"Unsupported store timezone: {timezoneId}");

        foreach (var candidate in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException($"Unsupported store timezone: {timezoneId}");
    }

    private static JObject GetStoreStatus(JObject store, DateTimeOffset utcNow)
    {
        var timezoneId = store["timezone_id"].ToString();
        var timezone = ResolveStoreTimeZone(timezoneId);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timezone);
        var day = localNow.DayOfWeek.ToString().ToLowerInvariant();
        var todayHours = OpeningHours.Value<string>(day);
        var parts = todayHours.Split('-');
        var opensAt = TimeSpan.Parse(parts[0]);
        var closesAt = TimeSpan.Parse(parts[1]);
        var isOpen = localNow.TimeOfDay >= opensAt && localNow.TimeOfDay < closesAt;

        return new JObject
        {
            ["store_local_time"] = localNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            ["today_hours"] = todayHours,
            ["opening_hours"] = OpeningHours.DeepClone(),
            ["is_open"] = isOpen
        };
    }

    // ── Tool Registration ───────────────────────────────────────────────

    private void RegisterCapabilities(McpRequestHandler handler)
    {
        // 1. get_store_location
        handler.AddTool("get_store_location",
            "Resolve a supported BlastBox pickup store and return its canonical location, address, coordinates, timezone, published opening hours, current local time, and open status.",
            schemaConfig: s => s.String("location", "Pickup store city and state: Springfield, IL; Los Angeles, CA; Seattle, WA; or Pixel Heights, WA.", required: true),
            handler: async (args, ct) =>
            {
                var location = (args.Value<string>("location") ?? "").Trim();
                var store = FindStore(location);
                if (store == null)
                    return UnknownStore();

                var result = new JObject
                {
                    ["location"] = store["location"],
                    ["name"] = store["name"],
                    ["address"] = store["address"],
                    ["lat"] = store["lat"],
                    ["lon"] = store["lon"],
                    ["timezone_id"] = store["timezone_id"]
                };
                result.Merge(GetStoreStatus(store, DateTimeOffset.UtcNow));
                return result;
            });

        // 2. list_bays
        handler.AddTool("list_bays",
            "Return one current planning snapshot of every curbside pickup bay, including cover, lighting, facing direction, wind protection, drainage, distance to the entrance, priority status, fixed demo occupancy, fixed orders_ahead from 6 through 10, and queue timing at 30 seconds per order ahead.",
            schemaConfig: s => s.String("location", "Pickup store city and state whose collection bays should be listed.", required: true),
            handler: async (args, ct) =>
            {
                var location = (args.Value<string>("location") ?? "").Trim();
                var store = FindStore(location);
                if (store == null)
                    return UnknownStore();

                var occupancy = store["occupancy"] as JObject;
                var queueObservedAt = TimeZoneInfo.ConvertTime(
                    DateTimeOffset.UtcNow,
                    ResolveStoreTimeZone(store["timezone_id"].ToString())
                ).ToString("yyyy-MM-ddTHH:mm:sszzz");
                var result = new JArray();
                foreach (var bay in (store["bays"] as JArray).OrderBy(b => b["bay_id"].Value<int>()))
                {
                    var bayId = bay["bay_id"].Value<int>();
                    var occupant = occupancy[bayId.ToString()];
                    var occupied = occupant != null && occupant.Type != JTokenType.Null && !string.IsNullOrEmpty(occupant.ToString());
                    var ordersAhead = bay["orders_ahead"].Value<int>();
                    result.Add(new JObject
                    {
                        ["bay_id"] = bayId,
                        ["covered"] = bay["covered"],
                        ["lit"] = bay["lit"],
                        ["facing"] = bay["facing"],
                        ["wind_protection"] = bay["wind_protection"],
                        ["drainage"] = bay["drainage"],
                        ["distance_to_entrance_m"] = bay["distance_to_entrance_m"],
                        ["priority"] = bay["priority"],
                        ["occupied"] = occupied,
                        ["occupied_by"] = occupied ? occupant : JValue.CreateNull(),
                        ["orders_ahead"] = ordersAhead,
                        ["queue_seconds_per_order"] = 30,
                        ["expected_wait_seconds"] = ordersAhead * 30,
                        ["expected_wait_minutes"] = ordersAhead / 2.0
                    });
                }
                return new JObject
                {
                    ["location"] = store["location"],
                    ["timezone_id"] = store["timezone_id"],
                    ["queue_observed_at"] = queueObservedAt,
                    ["bays"] = result
                };
            });

    }

}

// ║  SECTION 2: MCP FRAMEWORK                                                  ║
// ║                                                                            ║
// ║  Built-in McpRequestHandler that brings MCP C# SDK patterns to Power       ║
// ║  Platform. If Microsoft enables the official SDK namespaces, this section   ║
// ║  becomes a using statement instead of inline code.                          ║
// ║                                                                            ║
// ║  Spec coverage: MCP 2025-11-25                                             ║
// ║  Handles: initialize, ping, tools/*, resources/*, prompts/*,               ║
// ║           completion/complete, logging/setLevel, all notifications          ║
// ║                                                                            ║
// ║  Stateless limitations (Power Platform cannot send async notifications):   ║
// ║   - Tasks (experimental, requires persistent state between requests)       ║
// ║   - Server→client requests (sampling, elicitation, roots/list)             ║
// ║   - Server→client notifications (progress, logging/message, list_changed)  ║
// ║                                                                            ║
// ║  Do not modify unless extending the framework itself.                      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

// ── Configuration Types ──────────────────────────────────────────────────────

/// <summary>Server identity reported in initialize response.</summary>
public class McpServerInfo
{
    public string Name { get; set; } = "mcp-server";
    public string Version { get; set; } = "1.0.0";
    public string Title { get; set; }
    public string Description { get; set; }
}

/// <summary>Capabilities declared during initialization.</summary>
public class McpCapabilities
{
    public bool Tools { get; set; } = true;
    public bool Resources { get; set; }
    public bool Prompts { get; set; }
    public bool Logging { get; set; }
    public bool Completions { get; set; }
}

/// <summary>Top-level configuration for the MCP handler.</summary>
public class McpServerOptions
{
    public McpServerInfo ServerInfo { get; set; } = new McpServerInfo();
    public string ProtocolVersion { get; set; } = "2025-11-25";
    public McpCapabilities Capabilities { get; set; } = new McpCapabilities();
    public string Instructions { get; set; }
}

// ── Error Handling ───────────────────────────────────────────────────────────

/// <summary>Standard JSON-RPC 2.0 error codes used by MCP.</summary>
public enum McpErrorCode
{
    RequestTimeout = -32000,
    ParseError = -32700,
    InvalidRequest = -32600,
    MethodNotFound = -32601,
    InvalidParams = -32602,
    InternalError = -32603
}

/// <summary>
/// Throw from tool methods to surface a structured MCP error.
/// Mirrors ModelContextProtocol.McpException from the official SDK.
/// </summary>
public class McpException : Exception
{
    public McpErrorCode Code { get; }
    public McpException(McpErrorCode code, string message) : base(message) => Code = code;
}

// ── Schema Builder (Fluent API) ──────────────────────────────────────────────

/// <summary>Fluent builder for JSON Schema objects used in tool inputSchema.</summary>
public class McpSchemaBuilder
{
    private readonly JObject _properties = new JObject();
    private readonly JArray _required = new JArray();

    public McpSchemaBuilder String(string name, string description, bool required = false, string format = null, string[] enumValues = null)
    {
        var prop = new JObject { ["type"] = "string", ["description"] = description };
        if (format != null) prop["format"] = format;
        if (enumValues != null) prop["enum"] = new JArray(enumValues);
        _properties[name] = prop;
        if (required) _required.Add(name);
        return this;
    }

    public McpSchemaBuilder Integer(string name, string description, bool required = false, int? defaultValue = null)
    {
        var prop = new JObject { ["type"] = "integer", ["description"] = description };
        if (defaultValue.HasValue) prop["default"] = defaultValue.Value;
        _properties[name] = prop;
        if (required) _required.Add(name);
        return this;
    }

    public McpSchemaBuilder Number(string name, string description, bool required = false)
    {
        _properties[name] = new JObject { ["type"] = "number", ["description"] = description };
        if (required) _required.Add(name);
        return this;
    }

    public McpSchemaBuilder Boolean(string name, string description, bool required = false)
    {
        _properties[name] = new JObject { ["type"] = "boolean", ["description"] = description };
        if (required) _required.Add(name);
        return this;
    }

    public McpSchemaBuilder Array(string name, string description, JObject itemSchema, bool required = false)
    {
        _properties[name] = new JObject
        {
            ["type"] = "array",
            ["description"] = description,
            ["items"] = itemSchema
        };
        if (required) _required.Add(name);
        return this;
    }

    public McpSchemaBuilder Object(string name, string description, Action<McpSchemaBuilder> nestedConfig, bool required = false)
    {
        var nested = new McpSchemaBuilder();
        nestedConfig?.Invoke(nested);
        var obj = nested.Build();
        obj["description"] = description;
        _properties[name] = obj;
        if (required) _required.Add(name);
        return this;
    }

    public JObject Build()
    {
        var schema = new JObject
        {
            ["type"] = "object",
            ["properties"] = _properties
        };
        if (_required.Count > 0) schema["required"] = _required;
        return schema;
    }
}

// ── Internal Tool Registration ───────────────────────────────────────────────

internal class McpToolDefinition
{
    public string Name { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public JObject InputSchema { get; set; }
    public JObject OutputSchema { get; set; }
    public JObject Annotations { get; set; }
    public Func<JObject, CancellationToken, Task<object>> Handler { get; set; }
}

// ── Internal Resource Registration ───────────────────────────────────────────

internal class McpResourceDefinition
{
    public string Uri { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string MimeType { get; set; }
    public JObject Annotations { get; set; }
    public Func<CancellationToken, Task<JArray>> Handler { get; set; }
}

internal class McpResourceTemplateDefinition
{
    public string UriTemplate { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string MimeType { get; set; }
    public JObject Annotations { get; set; }
    public Func<string, CancellationToken, Task<JArray>> Handler { get; set; }
}

// ── Internal Prompt Registration ─────────────────────────────────────────────

/// <summary>Describes a single prompt argument.</summary>
public class McpPromptArgument
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Required { get; set; }
}

internal class McpPromptDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<McpPromptArgument> Arguments { get; set; } = new List<McpPromptArgument>();
    public Func<JObject, CancellationToken, Task<JArray>> Handler { get; set; }
}

// ── McpRequestHandler ────────────────────────────────────────────────────────
//
//    The core bridge class. Stateless, no DI, no ASP.NET Core.
//    Takes a JSON-RPC string in → returns a JSON-RPC string out.
//    This is the class that does not exist in the official SDK today.
//

/// <summary>
/// Stateless MCP request handler that bridges the official SDK's patterns
/// to Power Platform's ScriptBase.ExecuteAsync() model.
///
/// Handles all JSON-RPC 2.0 routing, protocol negotiation, tool discovery,
/// parameter binding, and response formatting internally.
/// </summary>
public class McpRequestHandler
{
    private readonly McpServerOptions _options;
    private readonly Dictionary<string, McpToolDefinition> _tools;
    private readonly Dictionary<string, McpResourceDefinition> _resources;
    private readonly List<McpResourceTemplateDefinition> _resourceTemplates;
    private readonly Dictionary<string, McpPromptDefinition> _prompts;

    /// <summary>
    /// Optional logging callback. Wire this up to Application Insights,
    /// Context.Logger, or any other telemetry sink.
    /// </summary>
    public Action<string, object> OnLog { get; set; }

    public McpRequestHandler(McpServerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tools = new Dictionary<string, McpToolDefinition>(StringComparer.OrdinalIgnoreCase);
        _resources = new Dictionary<string, McpResourceDefinition>(StringComparer.OrdinalIgnoreCase);
        _resourceTemplates = new List<McpResourceTemplateDefinition>();
        _prompts = new Dictionary<string, McpPromptDefinition>(StringComparer.OrdinalIgnoreCase);
    }

    // ── Tool Registration ────────────────────────────────────────────────

    /// <summary>
    /// Register a tool using the fluent API.
    /// Define the schema with McpSchemaBuilder, provide a handler, and optionally set annotations.
    /// </summary>
    public McpRequestHandler AddTool(
        string name,
        string description,
        Action<McpSchemaBuilder> schemaConfig,
        Func<JObject, CancellationToken, Task<JObject>> handler,
        Action<JObject> annotationsConfig = null,
        string title = null,
        Action<McpSchemaBuilder> outputSchemaConfig = null)
    {
        var builder = new McpSchemaBuilder();
        schemaConfig?.Invoke(builder);

        JObject annotations = null;
        if (annotationsConfig != null)
        {
            annotations = new JObject();
            annotationsConfig(annotations);
        }

        JObject outputSchema = null;
        if (outputSchemaConfig != null)
        {
            var outBuilder = new McpSchemaBuilder();
            outputSchemaConfig(outBuilder);
            outputSchema = outBuilder.Build();
        }

        _tools[name] = new McpToolDefinition
        {
            Name = name,
            Title = title,
            Description = description,
            InputSchema = builder.Build(),
            OutputSchema = outputSchema,
            Annotations = annotations,
            Handler = async (args, ct) => await handler(args, ct).ConfigureAwait(false)
        };

        return this;
    }

    // ── Resource Registration ─────────────────────────────────────────────

    /// <summary>
    /// Register a static resource. The handler returns the resource contents
    /// as a JArray of {uri, text, mimeType} or {uri, blob, mimeType} objects.
    /// </summary>
    public McpRequestHandler AddResource(
        string uri,
        string name,
        string description,
        Func<CancellationToken, Task<JArray>> handler,
        string mimeType = "application/json",
        Action<JObject> annotationsConfig = null)
    {
        JObject annotations = null;
        if (annotationsConfig != null)
        {
            annotations = new JObject();
            annotationsConfig(annotations);
        }

        _resources[uri] = new McpResourceDefinition
        {
            Uri = uri,
            Name = name,
            Description = description,
            MimeType = mimeType,
            Annotations = annotations,
            Handler = handler
        };

        return this;
    }

    /// <summary>
    /// Register a resource template. The handler receives the resolved URI
    /// and returns the resource contents as a JArray.
    /// </summary>
    public McpRequestHandler AddResourceTemplate(
        string uriTemplate,
        string name,
        string description,
        Func<string, CancellationToken, Task<JArray>> handler,
        string mimeType = "application/json",
        Action<JObject> annotationsConfig = null)
    {
        JObject annotations = null;
        if (annotationsConfig != null)
        {
            annotations = new JObject();
            annotationsConfig(annotations);
        }

        _resourceTemplates.Add(new McpResourceTemplateDefinition
        {
            UriTemplate = uriTemplate,
            Name = name,
            Description = description,
            MimeType = mimeType,
            Annotations = annotations,
            Handler = handler
        });

        return this;
    }

    // ── Prompt Registration ──────────────────────────────────────────────

    /// <summary>
    /// Register a prompt. The handler receives the argument values as a JObject
    /// and returns a JArray of message objects ({role, content: {type, text}}).
    /// </summary>
    public McpRequestHandler AddPrompt(
        string name,
        string description,
        List<McpPromptArgument> arguments,
        Func<JObject, CancellationToken, Task<JArray>> handler)
    {
        _prompts[name] = new McpPromptDefinition
        {
            Name = name,
            Description = description,
            Arguments = arguments ?? new List<McpPromptArgument>(),
            Handler = handler
        };

        return this;
    }

    // ── Main Handler ─────────────────────────────────────────────────────

    /// <summary>
    /// Process a raw JSON-RPC 2.0 request string and return a JSON-RPC response string.
    /// This is the single method that bridges the gap.
    /// </summary>
    public async Task<string> HandleAsync(string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body))
            return SerializeError(null, McpErrorCode.InvalidRequest, "Empty request body");

        JObject request;
        try
        {
            request = JObject.Parse(body);
        }
        catch (JsonException)
        {
            return SerializeError(null, McpErrorCode.ParseError, "Invalid JSON");
        }

        var method = request.Value<string>("method") ?? string.Empty;
        var id = request["id"];

        Log("McpRequestReceived", new { Method = method, HasId = id != null });

        try
        {
            switch (method)
            {
                // Core initialization
                case "initialize":
                    return HandleInitialize(id, request);

                // Notifications — Copilot Studio requires valid JSON-RPC for ALL requests
                case "initialized":
                case "notifications/initialized":
                case "notifications/cancelled":
                case "notifications/roots/list_changed":
                    return SerializeSuccess(id, new JObject());

                // Health check
                case "ping":
                    return SerializeSuccess(id, new JObject());

                // Tools
                case "tools/list":
                    return HandleToolsList(id);

                case "tools/call":
                    return await HandleToolsCallAsync(id, request, cancellationToken).ConfigureAwait(false);

                // Resources
                case "resources/list":
                    return HandleResourcesList(id);

                case "resources/templates/list":
                    return HandleResourceTemplatesList(id);

                case "resources/read":
                    return await HandleResourcesReadAsync(id, request, cancellationToken).ConfigureAwait(false);

                case "resources/subscribe":
                case "resources/unsubscribe":
                    return SerializeSuccess(id, new JObject());

                // Prompts
                case "prompts/list":
                    return HandlePromptsList(id);

                case "prompts/get":
                    return await HandlePromptsGetAsync(id, request, cancellationToken).ConfigureAwait(false);

                // Completions
                case "completion/complete":
                    return SerializeSuccess(id, new JObject
                    {
                        ["completion"] = new JObject
                        {
                            ["values"] = new JArray(),
                            ["total"] = 0,
                            ["hasMore"] = false
                        }
                    });

                // Logging level
                case "logging/setLevel":
                    return SerializeSuccess(id, new JObject());

                default:
                    Log("McpMethodNotFound", new { Method = method });
                    return SerializeError(id, McpErrorCode.MethodNotFound, "Method not found", method);
            }
        }
        catch (McpException ex)
        {
            Log("McpError", new { Method = method, Code = (int)ex.Code, Message = ex.Message });
            return SerializeError(id, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            Log("McpError", new { Method = method, Error = ex.Message });
            return SerializeError(id, McpErrorCode.InternalError, ex.Message);
        }
    }

    // ── Protocol Handlers ────────────────────────────────────────────────

    private string HandleInitialize(JToken id, JObject request)
    {
        var clientProtocolVersion = request["params"]?["protocolVersion"]?.ToString()
            ?? _options.ProtocolVersion;

        var capabilities = new JObject();
        if (_options.Capabilities.Tools)
            capabilities["tools"] = new JObject { ["listChanged"] = false };
        if (_options.Capabilities.Resources)
            capabilities["resources"] = new JObject { ["subscribe"] = false, ["listChanged"] = false };
        if (_options.Capabilities.Prompts)
            capabilities["prompts"] = new JObject { ["listChanged"] = false };
        if (_options.Capabilities.Logging)
            capabilities["logging"] = new JObject();
        if (_options.Capabilities.Completions)
            capabilities["completions"] = new JObject();

        var serverInfo = new JObject
        {
            ["name"] = _options.ServerInfo.Name,
            ["version"] = _options.ServerInfo.Version
        };
        if (!string.IsNullOrWhiteSpace(_options.ServerInfo.Title))
            serverInfo["title"] = _options.ServerInfo.Title;
        if (!string.IsNullOrWhiteSpace(_options.ServerInfo.Description))
            serverInfo["description"] = _options.ServerInfo.Description;

        var result = new JObject
        {
            ["protocolVersion"] = clientProtocolVersion,
            ["capabilities"] = capabilities,
            ["serverInfo"] = serverInfo
        };

        if (!string.IsNullOrWhiteSpace(_options.Instructions))
            result["instructions"] = _options.Instructions;

        Log("McpInitialized", new
        {
            Server = _options.ServerInfo.Name,
            Version = _options.ServerInfo.Version,
            ProtocolVersion = clientProtocolVersion
        });

        return SerializeSuccess(id, result);
    }

    private string HandleToolsList(JToken id)
    {
        var toolsArray = new JArray();
        foreach (var tool in _tools.Values)
        {
            var toolObj = new JObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema
            };
            if (!string.IsNullOrWhiteSpace(tool.Title))
                toolObj["title"] = tool.Title;
            if (tool.OutputSchema != null)
                toolObj["outputSchema"] = tool.OutputSchema;
            if (tool.Annotations != null && tool.Annotations.Count > 0)
                toolObj["annotations"] = tool.Annotations;
            toolsArray.Add(toolObj);
        }

        Log("McpToolsListed", new { Count = _tools.Count });
        return SerializeSuccess(id, new JObject { ["tools"] = toolsArray });
    }

    private string HandleResourcesList(JToken id)
    {
        var resourcesArray = new JArray();
        foreach (var res in _resources.Values)
        {
            var obj = new JObject
            {
                ["uri"] = res.Uri,
                ["name"] = res.Name
            };
            if (!string.IsNullOrWhiteSpace(res.Description))
                obj["description"] = res.Description;
            if (!string.IsNullOrWhiteSpace(res.MimeType))
                obj["mimeType"] = res.MimeType;
            if (res.Annotations != null && res.Annotations.Count > 0)
                obj["annotations"] = res.Annotations;
            resourcesArray.Add(obj);
        }

        Log("McpResourcesListed", new { Count = _resources.Count });
        return SerializeSuccess(id, new JObject { ["resources"] = resourcesArray });
    }

    private string HandleResourceTemplatesList(JToken id)
    {
        var templatesArray = new JArray();
        foreach (var tmpl in _resourceTemplates)
        {
            var obj = new JObject
            {
                ["uriTemplate"] = tmpl.UriTemplate,
                ["name"] = tmpl.Name
            };
            if (!string.IsNullOrWhiteSpace(tmpl.Description))
                obj["description"] = tmpl.Description;
            if (!string.IsNullOrWhiteSpace(tmpl.MimeType))
                obj["mimeType"] = tmpl.MimeType;
            if (tmpl.Annotations != null && tmpl.Annotations.Count > 0)
                obj["annotations"] = tmpl.Annotations;
            templatesArray.Add(obj);
        }

        Log("McpResourceTemplatesListed", new { Count = _resourceTemplates.Count });
        return SerializeSuccess(id, new JObject { ["resourceTemplates"] = templatesArray });
    }

    private async Task<string> HandleResourcesReadAsync(JToken id, JObject request, CancellationToken ct)
    {
        var paramsObj = request["params"] as JObject;
        var uri = paramsObj?.Value<string>("uri");

        if (string.IsNullOrWhiteSpace(uri))
            return SerializeError(id, McpErrorCode.InvalidParams, "Resource URI is required");

        // 1. Try exact match on registered static resources
        if (_resources.TryGetValue(uri, out var resource))
        {
            Log("McpResourceReadStarted", new { Uri = uri });
            try
            {
                var contents = await resource.Handler(ct).ConfigureAwait(false);
                Log("McpResourceReadCompleted", new { Uri = uri });
                return SerializeSuccess(id, new JObject { ["contents"] = contents });
            }
            catch (Exception ex)
            {
                Log("McpResourceReadError", new { Uri = uri, Error = ex.Message });
                return SerializeError(id, McpErrorCode.InternalError, ex.Message);
            }
        }

        // 2. Try matching against registered resource templates
        foreach (var tmpl in _resourceTemplates)
        {
            if (MatchesUriTemplate(tmpl.UriTemplate, uri))
            {
                Log("McpResourceReadStarted", new { Uri = uri, Template = tmpl.UriTemplate });
                try
                {
                    var contents = await tmpl.Handler(uri, ct).ConfigureAwait(false);
                    Log("McpResourceReadCompleted", new { Uri = uri });
                    return SerializeSuccess(id, new JObject { ["contents"] = contents });
                }
                catch (Exception ex)
                {
                    Log("McpResourceReadError", new { Uri = uri, Error = ex.Message });
                    return SerializeError(id, McpErrorCode.InternalError, ex.Message);
                }
            }
        }

        return SerializeError(id, McpErrorCode.InvalidParams, $"Resource not found: {uri}");
    }

    /// <summary>
    /// Simple URI template matcher. Checks if a concrete URI matches a template
    /// with {param} placeholders (e.g., "data://records/{id}" matches "data://records/123").
    /// </summary>
    private static bool MatchesUriTemplate(string template, string uri)
    {
        // Split both on '/' and compare segments
        var templateParts = template.Split('/');
        var uriParts = uri.Split('/');

        if (templateParts.Length != uriParts.Length) return false;

        for (int i = 0; i < templateParts.Length; i++)
        {
            var seg = templateParts[i];
            if (seg.StartsWith("{") && seg.EndsWith("}")) continue; // wildcard
            if (!string.Equals(seg, uriParts[i], StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    /// <summary>
    /// Extract named parameters from a URI given a template pattern.
    /// E.g., template "data://records/{id}" with uri "data://records/123" returns { "id": "123" }.
    /// </summary>
    public static Dictionary<string, string> ExtractUriParameters(string template, string uri)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var templateParts = template.Split('/');
        var uriParts = uri.Split('/');

        if (templateParts.Length != uriParts.Length) return result;

        for (int i = 0; i < templateParts.Length; i++)
        {
            var seg = templateParts[i];
            if (seg.StartsWith("{") && seg.EndsWith("}"))
            {
                var paramName = seg.Substring(1, seg.Length - 2);
                result[paramName] = uriParts[i];
            }
        }
        return result;
    }

    private string HandlePromptsList(JToken id)
    {
        var promptsArray = new JArray();
        foreach (var prompt in _prompts.Values)
        {
            var obj = new JObject
            {
                ["name"] = prompt.Name
            };
            if (!string.IsNullOrWhiteSpace(prompt.Description))
                obj["description"] = prompt.Description;

            if (prompt.Arguments.Count > 0)
            {
                var argsArray = new JArray();
                foreach (var arg in prompt.Arguments)
                {
                    var argObj = new JObject { ["name"] = arg.Name };
                    if (!string.IsNullOrWhiteSpace(arg.Description))
                        argObj["description"] = arg.Description;
                    if (arg.Required)
                        argObj["required"] = true;
                    argsArray.Add(argObj);
                }
                obj["arguments"] = argsArray;
            }

            promptsArray.Add(obj);
        }

        Log("McpPromptsListed", new { Count = _prompts.Count });
        return SerializeSuccess(id, new JObject { ["prompts"] = promptsArray });
    }

    private async Task<string> HandlePromptsGetAsync(JToken id, JObject request, CancellationToken ct)
    {
        var paramsObj = request["params"] as JObject;
        var promptName = paramsObj?.Value<string>("name");
        var arguments = paramsObj?["arguments"] as JObject ?? new JObject();

        if (string.IsNullOrWhiteSpace(promptName))
            return SerializeError(id, McpErrorCode.InvalidParams, "Prompt name is required");

        if (!_prompts.TryGetValue(promptName, out var prompt))
            return SerializeError(id, McpErrorCode.InvalidParams, $"Prompt not found: {promptName}");

        Log("McpPromptGetStarted", new { Prompt = promptName });

        try
        {
            var messages = await prompt.Handler(arguments, ct).ConfigureAwait(false);
            Log("McpPromptGetCompleted", new { Prompt = promptName, MessageCount = messages.Count });

            var result = new JObject { ["messages"] = messages };
            if (!string.IsNullOrWhiteSpace(prompt.Description))
                result["description"] = prompt.Description;

            return SerializeSuccess(id, result);
        }
        catch (Exception ex)
        {
            Log("McpPromptGetError", new { Prompt = promptName, Error = ex.Message });
            return SerializeError(id, McpErrorCode.InternalError, ex.Message);
        }
    }

    private async Task<string> HandleToolsCallAsync(JToken id, JObject request, CancellationToken ct)
    {
        var paramsObj = request["params"] as JObject;
        var toolName = paramsObj?.Value<string>("name");
        var arguments = paramsObj?["arguments"] as JObject ?? new JObject();

        if (string.IsNullOrWhiteSpace(toolName))
            return SerializeError(id, McpErrorCode.InvalidParams, "Tool name is required");

        if (!_tools.TryGetValue(toolName, out var tool))
            return SerializeError(id, McpErrorCode.InvalidParams, $"Unknown tool: {toolName}");

        Log("McpToolCallStarted", new { Tool = toolName });

        try
        {
            var result = await tool.Handler(arguments, ct).ConfigureAwait(false);

            JObject callResult;

            // Support pre-formatted MCP tool results with rich content types
            // (image, audio, resource, or mixed content arrays).
            // If the handler returns { "content": [ { "type": "..." } ], ... },
            // pass it through directly instead of wrapping in text.
            if (result is JObject jobj && jobj["content"] is JArray contentArray
                && contentArray.Count > 0 && contentArray[0]?["type"] != null)
            {
                callResult = new JObject
                {
                    ["content"] = contentArray,
                    ["isError"] = jobj.Value<bool?>("isError") ?? false
                };
                if (jobj["structuredContent"] is JObject structured)
                    callResult["structuredContent"] = structured;
            }
            else
            {
                string text;
                if (result is JObject plainObj)
                    text = plainObj.ToString(Newtonsoft.Json.Formatting.Indented);
                else if (result is string s)
                    text = s;
                else if (result == null)
                    text = "{}";
                else
                    text = JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);

                callResult = new JObject
                {
                    ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = text } },
                    ["isError"] = false
                };
            }

            Log("McpToolCallCompleted", new { Tool = toolName, IsError = callResult.Value<bool>("isError") });
            return SerializeSuccess(id, callResult);
        }
        catch (ArgumentException ex)
        {
            return SerializeSuccess(id, new JObject
            {
                ["content"] = new JArray
                {
                    new JObject { ["type"] = "text", ["text"] = $"Invalid arguments: {ex.Message}" }
                },
                ["isError"] = true
            });
        }
        catch (McpException ex)
        {
            return SerializeSuccess(id, new JObject
            {
                ["content"] = new JArray
                {
                    new JObject { ["type"] = "text", ["text"] = $"Tool error: {ex.Message}" }
                },
                ["isError"] = true
            });
        }
        catch (Exception ex)
        {
            Log("McpToolCallError", new { Tool = toolName, Error = ex.Message });

            return SerializeSuccess(id, new JObject
            {
                ["content"] = new JArray
                {
                    new JObject { ["type"] = "text", ["text"] = $"Tool execution failed: {ex.Message}" }
                },
                ["isError"] = true
            });
        }
    }

    // ── Content Helpers ────────────────────────────────────────────────
    //
    //    Use these to build rich tool results with image, audio, or resource
    //    content. Return McpRequestHandler.ToolResult(...) from your handler
    //    to bypass automatic text wrapping.
    //

    /// <summary>Create a text content item.</summary>
    public static JObject TextContent(string text) =>
        new JObject { ["type"] = "text", ["text"] = text };

    /// <summary>Create an image content item (base64-encoded).</summary>
    public static JObject ImageContent(string base64Data, string mimeType) =>
        new JObject { ["type"] = "image", ["data"] = base64Data, ["mimeType"] = mimeType };

    /// <summary>Create an audio content item (base64-encoded).</summary>
    public static JObject AudioContent(string base64Data, string mimeType) =>
        new JObject { ["type"] = "audio", ["data"] = base64Data, ["mimeType"] = mimeType };

    /// <summary>Create an embedded resource content item.</summary>
    public static JObject ResourceContent(string uri, string text, string mimeType = "text/plain") =>
        new JObject
        {
            ["type"] = "resource",
            ["resource"] = new JObject { ["uri"] = uri, ["text"] = text, ["mimeType"] = mimeType }
        };

    /// <summary>
    /// Build a pre-formatted tool result with mixed content types.
    /// Return this from a tool handler to bypass automatic text wrapping.
    /// </summary>
    public static JObject ToolResult(JArray content, JObject structuredContent = null, bool isError = false)
    {
        var result = new JObject { ["content"] = content, ["isError"] = isError };
        if (structuredContent != null) result["structuredContent"] = structuredContent;
        return result;
    }

    // ── JSON-RPC Serialization ───────────────────────────────────────────

    private string SerializeSuccess(JToken id, JObject result)
    {
        return new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        }.ToString(Newtonsoft.Json.Formatting.None);
    }

    private string SerializeError(JToken id, McpErrorCode code, string message, string data = null)
    {
        return SerializeError(id, (int)code, message, data);
    }

    private string SerializeError(JToken id, int code, string message, string data = null)
    {
        var error = new JObject
        {
            ["code"] = code,
            ["message"] = message
        };
        if (!string.IsNullOrWhiteSpace(data))
            error["data"] = data;

        return new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = error
        }.ToString(Newtonsoft.Json.Formatting.None);
    }

    private void Log(string eventName, object data)
    {
        OnLog?.Invoke(eventName, data);
    }
}

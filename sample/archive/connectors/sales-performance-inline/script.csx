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
// ║  Sales & Performance MCP Connector (inline)                                 ║
// ║                                                                            ║
// ║  RAW, queryable retail data only — NO pre-baked analytics.                 ║
// ║  The agent constructs its own queries and derives velocity, weeks-of-cover, ║
// ║  and margin itself. Tools: query_sales, get_catalog, apply_markdown.        ║
// ║  Based on Power MCP Template v2.1 by Troy Taylor.                          ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

public class Script : ScriptBase
{


    private static readonly McpServerOptions Options = new McpServerOptions
    {
        ServerInfo = new McpServerInfo
        {
            Name = "sales-performance",
            Version = "1.0.0",
            Title = "Sales and Performance",
            Description = "Raw, queryable sales and catalog data for a retail store. Returns weekly unit/revenue rows and product catalog facts (price, unit cost, stock on hand). Contains NO pre-computed analytics — the caller constructs queries and derives velocity, weeks-of-cover, and margin."
        },
        ProtocolVersion = "2025-11-25",
        Capabilities = new McpCapabilities { Tools = true },
        Instructions = "Use query_sales(start_week, end_week, sku?, category?) to pull RAW weekly rows of units sold and revenue for a date window; weeks are week-ending dates from 2026-04-12 to 2026-05-31. Use get_catalog(category?, sku?) for price, unit_cost, and stock_on_hand. This server returns raw data only — construct your own queries and compute velocity (units/weeks), weeks-of-cover (stock/velocity), and margin ((price-cost)/price) yourself. Once a markdown is decided and checked against policy, call apply_markdown(sku, new_price|discount_pct, markdown_type?, effective_date?) once per product to enact the price change and get a confirmation receipt."
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

    // Week-ending dates (ISO, lexically sortable). 8 trailing weeks.
    private static readonly string[] Weeks = new[]
    {
        "2026-04-12","2026-04-19","2026-04-26","2026-05-03",
        "2026-05-10","2026-05-17","2026-05-24","2026-05-31"
    };

    private static readonly JArray Catalog = JArray.Parse(@"[
        {""sku"":""SKU-OMEGA-MEGA"",""name"":""BlastBox Omega MEGA Edition"",""category"":""console"",""price"":499.99,""unit_cost"":300.00,""stock_on_hand"":60},
        {""sku"":""SKU-MEGALIZARDS"",""name"":""MEGA Lizards from Outer Space"",""category"":""game"",""price"":69.99,""unit_cost"":25.00,""stock_on_hand"":90},
        {""sku"":""SKU-PULSE-CTRL"",""name"":""PulseGrip Pro Controller"",""category"":""accessory"",""price"":59.99,""unit_cost"":22.00,""stock_on_hand"":120},
        {""sku"":""SKU-OMEGA-CORE"",""name"":""BlastBox Omega Core (1st-gen)"",""category"":""console"",""price"":399.99,""unit_cost"":320.00,""stock_on_hand"":75},
        {""sku"":""SKU-RETRO-CADET"",""name"":""BlastBox Cadet Bundle"",""category"":""console"",""price"":149.99,""unit_cost"":110.00,""stock_on_hand"":60},
        {""sku"":""SKU-GALAXY-SMASH"",""name"":""Galaxy Smash"",""category"":""game"",""price"":49.99,""unit_cost"":18.00,""stock_on_hand"":140},
        {""sku"":""SKU-VR-GOGGLES"",""name"":""OmegaVision VR Headset"",""category"":""accessory"",""price"":199.99,""unit_cost"":70.00,""stock_on_hand"":48}
    ]");

    // Weekly units sold per SKU, aligned to Weeks[] (8 values each).
    private static readonly JObject WeeklyUnits = JObject.Parse(@"{
        ""SKU-OMEGA-MEGA"":[38,42,40,36,44,40,39,41],
        ""SKU-MEGALIZARDS"":[60,72,68,64,70,66,71,69],
        ""SKU-PULSE-CTRL"":[48,52,50,49,53,51,54,53],
        ""SKU-OMEGA-CORE"":[9,8,7,6,6,5,4,3],
        ""SKU-RETRO-CADET"":[2,1,1,1,2,1,1,0],
        ""SKU-GALAXY-SMASH"":[4,3,2,3,2,3,2,3],
        ""SKU-VR-GOGGLES"":[1,1,0,1,1,0,1,1]
    }");

    // ── Tool Registration ───────────────────────────────────────────────

    private void RegisterCapabilities(McpRequestHandler handler)
    {
        // 1. query_sales
        handler.AddTool("query_sales",
            "Return RAW weekly sales rows (units sold and revenue) for a date window. Each row has sku, name, category, week, units, and revenue fields. Weeks are week-ending dates between 2026-04-12 and 2026-05-31. Optionally filter by a single sku or a category. This is raw data - compute velocity, weeks-of-cover, and margin yourself.",
            schemaConfig: s => s
                .String("start_week", "Inclusive start week-ending date (YYYY-MM-DD). Omit for the earliest available week (2026-04-12).")
                .String("end_week", "Inclusive end week-ending date (YYYY-MM-DD). Omit for the latest available week (2026-05-31).")
                .String("sku", "Optional. Restrict to a single product SKU (e.g. 'SKU-VR-GOGGLES').")
                .String("category", "Optional. Restrict to a category: 'console', 'game', or 'accessory'."),
            handler: async (args, ct) =>
            {
                var start = (args.Value<string>("start_week") ?? "").Trim();
                if (start.Length == 0) start = Weeks[0];
                var end = (args.Value<string>("end_week") ?? "").Trim();
                if (end.Length == 0) end = Weeks[Weeks.Length - 1];
                var skuFilter = (args.Value<string>("sku") ?? "").Trim();
                var catFilter = (args.Value<string>("category") ?? "").Trim().ToLowerInvariant();

                int startIdx = 0;
                int endIdx = Weeks.Length - 1;
                for (int i = 0; i < Weeks.Length; i++)
                {
                    if (Weeks[i] == start) startIdx = i;
                    if (Weeks[i] == end) endIdx = i;
                }

                var rows = new JArray();
                foreach (var item in Catalog)
                {
                    var sku = item["sku"]?.ToString() ?? "";
                    var cat = (item["category"]?.ToString() ?? "").ToLowerInvariant();
                    var price = item["price"]?.Value<double>() ?? 0.0;
                    if (skuFilter.Length > 0 && !string.Equals(sku, skuFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    if (catFilter.Length > 0 && cat != catFilter) continue;

                    var units = WeeklyUnits[sku] as JArray;
                    if (units == null) continue;
                    for (int w = startIdx; w <= endIdx; w++)
                    {
                        var week = Weeks[w];
                        var u = units[w].Value<int>();
                        var revenue = ((long)(u * price * 100.0 + 0.5)) / 100.0;
                        rows.Add(new JObject
                        {
                            ["sku"] = sku,
                            ["name"] = item["name"],
                            ["category"] = item["category"],
                            ["week"] = week,
                            ["units"] = u,
                            ["revenue"] = revenue
                        });
                    }
                }

                return new JObject
                {
                    ["start_week"] = start,
                    ["end_week"] = end,
                    ["row_count"] = rows.Count,
                    ["rows"] = rows
                };
            });

        // 2. get_catalog
        handler.AddTool("get_catalog",
            "Return catalog facts for products: sku, name, category, price, unit_cost, and stock_on_hand. Optionally filter by category or a single sku. Use price and unit_cost to derive margin, and stock_on_hand with your derived velocity to derive weeks-of-cover.",
            schemaConfig: s => s
                .String("category", "Optional. Restrict to a category: 'console', 'game', or 'accessory'.")
                .String("sku", "Optional. Restrict to a single product SKU."),
            handler: async (args, ct) =>
            {
                var skuFilter = (args.Value<string>("sku") ?? "").Trim();
                var catFilter = (args.Value<string>("category") ?? "").Trim().ToLowerInvariant();

                var items = new JArray();
                foreach (var item in Catalog)
                {
                    var sku = item["sku"]?.ToString() ?? "";
                    var cat = (item["category"]?.ToString() ?? "").ToLowerInvariant();
                    if (skuFilter.Length > 0 && !string.Equals(sku, skuFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    if (catFilter.Length > 0 && cat != catFilter) continue;
                    items.Add(JObject.Parse(item.ToString()));
                }
                return new JObject { ["count"] = items.Count, ["items"] = items };
            });

        // 3. apply_markdown
        handler.AddTool("apply_markdown",
            "Enact a price markdown for a single SKU. Call this once per product after the markdown has been decided and checked against policy. Provide either new_price or discount_pct (not both). Returns an applied-price confirmation receipt. Prices below unit_cost are rejected unless markdown_type is 'clearance' (clearance also requires prior manager approval).",
            schemaConfig: s => s
                .String("sku", "Required. The product SKU to mark down (e.g. 'SKU-VR-GOGGLES').", required: true)
                .Number("new_price", "The new shelf price in dollars. Provide this OR discount_pct.")
                .Number("discount_pct", "Percent off the current price as a whole number (e.g. 30 for 30%). Provide this OR new_price.")
                .String("markdown_type", "'promo' (default) or 'clearance'. Clearance is required to price below unit cost and assumes manager approval was already granted.", enumValues: new[] { "promo", "clearance" })
                .String("effective_date", "Optional date the new price takes effect (YYYY-MM-DD). Defaults to today."),
            handler: async (args, ct) =>
            {
                var skuArg = (args.Value<string>("sku") ?? "").Trim();
                if (skuArg.Length == 0)
                    return new JObject { ["error"] = "sku is required." };

                JToken item = null;
                foreach (var it in Catalog)
                {
                    if (string.Equals(it["sku"]?.ToString() ?? "", skuArg, StringComparison.OrdinalIgnoreCase))
                    {
                        item = it;
                        break;
                    }
                }
                if (item == null)
                    return new JObject { ["error"] = $"Unknown sku '{skuArg}'. Use get_catalog to list valid SKUs." };

                var oldPrice = item["price"]?.Value<double>() ?? 0.0;
                var cost = item["unit_cost"]?.Value<double>() ?? 0.0;

                var hasNewPrice = args["new_price"] != null && args["new_price"].Type != JTokenType.Null;
                var hasDiscount = args["discount_pct"] != null && args["discount_pct"].Type != JTokenType.Null;
                if (hasNewPrice == hasDiscount)
                    return new JObject { ["error"] = "Provide exactly one of new_price or discount_pct." };

                double newPrice;
                if (hasNewPrice)
                {
                    newPrice = args.Value<double>("new_price");
                }
                else
                {
                    var pct = args.Value<double>("discount_pct");
                    if (pct < 0 || pct >= 100)
                        return new JObject { ["error"] = "discount_pct must be between 0 and 100." };
                    newPrice = oldPrice * (1.0 - pct / 100.0);
                }

                newPrice = Math.Round(newPrice, 2, MidpointRounding.AwayFromZero);
                if (newPrice <= 0)
                    return new JObject { ["error"] = "Resulting price must be greater than zero." };
                if (newPrice >= oldPrice)
                    return new JObject { ["error"] = "A markdown must lower the price below the current price." };

                var markdownType = (args.Value<string>("markdown_type") ?? "promo").Trim().ToLowerInvariant();
                if (markdownType != "promo" && markdownType != "clearance") markdownType = "promo";

                if (newPrice < cost && markdownType != "clearance")
                    return new JObject
                    {
                        ["error"] = $"Price {newPrice:0.00} is below unit cost {cost:0.00}. A below-cost markdown must be run as a clearance (markdown_type='clearance') with manager approval, not a promo."
                    };

                var effective = (args.Value<string>("effective_date") ?? "").Trim();
                if (effective.Length == 0) effective = DateTime.UtcNow.ToString("yyyy-MM-dd");

                var discountPctEffective = Math.Round((oldPrice - newPrice) / oldPrice * 100.0, 1, MidpointRounding.AwayFromZero);
                var newMargin = newPrice > 0 ? Math.Round((newPrice - cost) / newPrice * 100.0, 1, MidpointRounding.AwayFromZero) : 0.0;
                var skuSuffix = skuArg.Replace("SKU-", "").Replace("-", "");
                var confirmationId = $"MKD-{skuSuffix}-{DateTime.UtcNow:yyyyMMddHHmmss}";

                return new JObject
                {
                    ["status"] = "applied",
                    ["confirmation_id"] = confirmationId,
                    ["sku"] = skuArg,
                    ["name"] = item["name"],
                    ["markdown_type"] = markdownType,
                    ["old_price"] = oldPrice,
                    ["new_price"] = newPrice,
                    ["discount_pct"] = discountPctEffective,
                    ["new_margin_pct"] = newMargin,
                    ["effective_date"] = effective,
                    ["message"] = $"Markdown applied to {item["name"]}: {oldPrice:0.00} -> {newPrice:0.00} ({discountPctEffective:0.#}% off), effective {effective}."
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

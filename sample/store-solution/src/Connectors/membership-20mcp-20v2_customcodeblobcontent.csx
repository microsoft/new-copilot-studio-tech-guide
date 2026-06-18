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
// ║  Membership MCP Connector (inline)                                          ║
// ║                                                                            ║
// ║  BlastPass membership lookup + cancellation for the BlastBox Omega console.║
// ║  Give it a mega-blast-customer-id and it returns the membership tier and   ║
// ║  how many whole months are left on the term — the number the Returns &     ║
// ║  Service Assistant feeds into its prorated-refund Python calculation.      ║
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
            Name = "blastpass-membership",
            Version = "1.0.0",
            Title = "BlastPass Membership",
            Description = "Look up a BlastPass console membership by its mega-blast-customer-id, cancel memberships, and reissue lost/stolen/damaged membership cards. Returns the membership tier, activation date, hours streamed, the card_serial on file, and how many whole months remain on the 12-month term — the inputs needed to calculate a prorated cancellation refund or print a replacement card."
        },
        ProtocolVersion = "2025-11-25",
        Capabilities = new McpCapabilities { Tools = true },
        Instructions = "Use get_membership with a mega-blast-customer-id (e.g. 'MEGA-BLAST-1024') to retrieve the member's BlastPass tier, annual price, activation date, hours streamed, card_serial, and months_remaining on the term. Pass months_remaining and the price to the prorated-refund calculation. Use cancel_membership once a refund figure is agreed to close the membership and get a confirmation number. Use reissue_card for a self-serve lost/stolen/damaged card replacement: it deactivates the old card_serial and issues a new one while leaving the membership active — feed the returned new_card_serial into the membership-card-png skill to give the member a digital card to save while the physical one ships."
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
    //
    // months_remaining is the authoritative count of WHOLE unused months left
    // on the 12-month term, pre-computed so the demo is deterministic. The
    // prorated refund = months_remaining x (annual_price / 12) - cancellation_fee
    // - nonrefundable_credit, unless the cooling-off rule applies (activated <= 14
    // days ago AND hours_streamed < 2 -> full refund of annual_price).

    private static readonly JArray Memberships = JArray.Parse(@"[
        {
            ""customer_id"":""MEGA-BLAST-1024"",
            ""member_name"":""Jordan Pixel"",
            ""tier"":""BlastPass Plus Extra"",
            ""tier_code"":""extra"",
            ""status"":""active"",
            ""annual_price"":129.99,
            ""cancellation_fee"":10.00,
            ""nonrefundable_credit"":0.00,
            ""activation_date"":""2026-02-26"",
            ""term_end_date"":""2027-02-26"",
            ""months_remaining"":8,
            ""hours_streamed"":47.5,
            ""console_serial"":""OMEGA-7F3A-1024"",
            ""card_serial"":""BLAST-7F3A-1024""
        },
        {
            ""customer_id"":""MEGA-BLAST-2048"",
            ""member_name"":""Sam Sparkle"",
            ""tier"":""BlastPass Plus Extra MEGA!!!"",
            ""tier_code"":""mega"",
            ""status"":""active"",
            ""annual_price"":199.99,
            ""cancellation_fee"":25.00,
            ""nonrefundable_credit"":20.00,
            ""activation_date"":""2026-05-30"",
            ""term_end_date"":""2027-05-30"",
            ""months_remaining"":11,
            ""hours_streamed"":1.2,
            ""console_serial"":""OMEGA-9B1C-2048"",
            ""card_serial"":""BLAST-9B1C-2048""
        },
        {
            ""customer_id"":""MEGA-BLAST-4096"",
            ""member_name"":""Riley Retro"",
            ""tier"":""BlastPass Plus"",
            ""tier_code"":""plus"",
            ""status"":""active"",
            ""annual_price"":79.99,
            ""cancellation_fee"":0.00,
            ""nonrefundable_credit"":0.00,
            ""activation_date"":""2025-12-10"",
            ""term_end_date"":""2026-12-10"",
            ""months_remaining"":6,
            ""hours_streamed"":88.0,
            ""console_serial"":""OMEGA-2D4E-4096"",
            ""card_serial"":""BLAST-2D4E-4096""
        },
        {
            ""customer_id"":""MEGA-BLAST-8192"",
            ""member_name"":""Casey Combo"",
            ""tier"":""BlastPass Plus Extra MEGA!!!"",
            ""tier_code"":""mega"",
            ""status"":""active"",
            ""annual_price"":199.99,
            ""cancellation_fee"":25.00,
            ""nonrefundable_credit"":20.00,
            ""activation_date"":""2026-01-05"",
            ""term_end_date"":""2027-01-05"",
            ""months_remaining"":5,
            ""hours_streamed"":210.0,
            ""console_serial"":""OMEGA-5A6F-8192"",
            ""card_serial"":""BLAST-5A6F-8192""
        }
    ]");

    // ── Tool Registration ───────────────────────────────────────────────

    private void RegisterCapabilities(McpRequestHandler handler)
    {
        // 1. get_membership
        handler.AddTool("get_membership",
            "Look up a BlastPass membership by its mega-blast-customer-id. Returns the membership tier, annual price, activation date, hours streamed, cancellation fee, any non-refundable credit, and months_remaining (the whole unused months left on the 12-month term). Feed months_remaining and annual_price into the prorated-refund calculation.",
            schemaConfig: s => s.String("customer_id", "The mega-blast-customer-id (e.g. 'MEGA-BLAST-1024'). Case-insensitive.", required: true),
            handler: async (args, ct) =>
            {
                var id = (args.Value<string>("customer_id") ?? "").Trim();
                foreach (var m in Memberships)
                {
                    if (string.Equals(m["customer_id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase))
                    {
                        var activation = DateTime.Parse(m["activation_date"].ToString());
                        var daysSince = (int)(DateTime.UtcNow.Date - activation.Date).TotalDays;
                        var hours = m["hours_streamed"]?.Value<double>() ?? 0;
                        var coolingOff = daysSince <= 14 && daysSince >= 0 && hours < 2;
                        return new JObject
                        {
                            ["customer_id"] = m["customer_id"],
                            ["member_name"] = m["member_name"],
                            ["tier"] = m["tier"],
                            ["tier_code"] = m["tier_code"],
                            ["status"] = m["status"],
                            ["annual_price"] = m["annual_price"],
                            ["cancellation_fee"] = m["cancellation_fee"],
                            ["nonrefundable_credit"] = m["nonrefundable_credit"],
                            ["activation_date"] = m["activation_date"],
                            ["term_end_date"] = m["term_end_date"],
                            ["days_since_activation"] = daysSince,
                            ["months_remaining"] = m["months_remaining"],
                            ["hours_streamed"] = m["hours_streamed"],
                            ["within_cooling_off"] = coolingOff,
                            ["console_serial"] = m["console_serial"],
                            ["card_serial"] = m["card_serial"]
                        };
                    }
                }
                throw new ArgumentException($"No BlastPass membership found for customer id \"{id}\". Check the mega-blast-customer-id (format: MEGA-BLAST-####).");
            });

        // 2. cancel_membership
        handler.AddTool("cancel_membership",
            "Cancel a BlastPass membership after a refund amount has been agreed. Records the cancellation and returns a confirmation number, the effective date, and the refund amount on file. Only call this once the associate confirms the customer wants to proceed.",
            schemaConfig: s => s
                .String("customer_id", "The mega-blast-customer-id of the membership to cancel (e.g. 'MEGA-BLAST-1024').", required: true)
                .Number("refund_amount", "The agreed prorated refund amount in dollars, as calculated by the refund step.", required: true),
            handler: async (args, ct) =>
            {
                var id = (args.Value<string>("customer_id") ?? "").Trim();
                var refund = args.Value<double?>("refund_amount") ?? 0.0;
                foreach (var m in Memberships)
                {
                    if (string.Equals(m["customer_id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase))
                    {
                        var confirmation = "BPX-CXL-" + id.Replace("MEGA-BLAST-", "") + "-" + DateTime.UtcNow.ToString("yyyyMMdd");
                        return new JObject
                        {
                            ["customer_id"] = m["customer_id"],
                            ["member_name"] = m["member_name"],
                            ["tier"] = m["tier"],
                            ["previous_status"] = m["status"],
                            ["new_status"] = "cancelled",
                            ["cancellation_confirmation"] = confirmation,
                            ["effective_date"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                            ["refund_amount"] = Math.Round(refund, 2),
                            ["message"] = $"BlastPass {m["tier"]} membership for {m["member_name"]} is cancelled. A refund of ${Math.Round(refund, 2):0.00} will be issued to the original payment method."
                        };
                    }
                }
                throw new ArgumentException($"No BlastPass membership found for customer id \"{id}\". Cannot cancel.");
            });

        // 3. reissue_card
        handler.AddTool("reissue_card",
            "Deactivate a member's current physical BlastPass card and issue a replacement, WITHOUT cancelling or changing the membership itself. Use this for a self-serve lost / stolen / damaged card request: it invalidates the old card_serial, mints a brand-new card_serial, keeps the membership active, and queues the new physical card for mailing. Returns the previous (now void) card_serial, the new card_serial to print on a digital card, a reissue confirmation number, and the mailing ETA for the physical card. The membership tier, term, and BlastPoints are untouched.",
            schemaConfig: s => s
                .String("customer_id", "The mega-blast-customer-id whose card is being replaced (e.g. 'MEGA-BLAST-1024').", required: true)
                .String("reason", "Why the card is being reissued: 'lost', 'stolen', or 'damaged'. Defaults to 'lost'.", required: false),
            handler: async (args, ct) =>
            {
                var id = (args.Value<string>("customer_id") ?? "").Trim();
                var reason = (args.Value<string>("reason") ?? "lost").Trim().ToLowerInvariant();
                foreach (var m in Memberships)
                {
                    if (string.Equals(m["customer_id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.Equals(m["status"]?.ToString(), "active", StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException($"BlastPass membership for {m["member_name"]} is {m["status"]}, so a card cannot be reissued.");

                        var prevCard = m["card_serial"]?.ToString() ?? "";
                        // Mint a new card_serial: keep the trailing member segment, refresh the middle block.
                        var tail = id.Replace("MEGA-BLAST-", "");
                        var seed = (prevCard + DateTime.UtcNow.Ticks).GetHashCode();
                        var block = ((seed & 0x7FFFFFFF) % 0x10000).ToString("X4");
                        var newCard = $"BLAST-{block}-{tail}";
                        // Persist so a follow-up get_membership reflects the new card.
                        m["card_serial"] = newCard;

                        var confirmation = "BPX-CARD-" + tail + "-" + DateTime.UtcNow.ToString("yyyyMMdd");
                        var shipDays = 7;
                        var eta = DateTime.UtcNow.Date.AddDays(shipDays);
                        var last4 = prevCard.Length >= 4 ? prevCard.Substring(prevCard.Length - 4) : prevCard;
                        return new JObject
                        {
                            ["customer_id"] = m["customer_id"],
                            ["member_name"] = m["member_name"],
                            ["tier"] = m["tier"],
                            ["tier_code"] = m["tier_code"],
                            ["reason"] = reason,
                            ["previous_card_serial"] = prevCard,
                            ["previous_card_status"] = "deactivated",
                            ["new_card_serial"] = newCard,
                            ["reissue_confirmation"] = confirmation,
                            ["membership_status"] = m["status"],
                            ["digital_card_ready"] = true,
                            ["ship_method"] = "Standard mail",
                            ["ship_eta_days"] = shipDays,
                            ["estimated_delivery"] = eta.ToString("yyyy-MM-dd"),
                            ["message"] = $"Your old card ending {last4} is now deactivated and can no longer be used. A new {m["tier"]} card ({newCard}) has been issued — a printed copy is on its way by standard mail (arriving ~{eta:yyyy-MM-dd}). You can save the digital card now and use it in-store and at checkout right away."
                        };
                    }
                }
                throw new ArgumentException($"No BlastPass membership found for customer id \"{id}\". Cannot reissue a card.");
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

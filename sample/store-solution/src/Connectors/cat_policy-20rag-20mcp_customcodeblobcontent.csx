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
// ║  Policy RAG MCP Connector (inline, MOCK)                                    ║
// ║                                                                            ║
// ║  A pretend "knowledge base" for the BlastBox Omega Store Policy Agent.      ║
// ║  Modern Copilot Studio agents can't take uploaded files, so the tier        ║
// ║  policy booklets live here as static passages and a mock keyword search.   ║
// ║                                                                            ║
// ║  No embeddings, no external store — just canned text + naive scoring.       ║
// ║  Based on Power MCP Template v2.1 by Troy Taylor.                          ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

public class Script : ScriptBase
{


    private static readonly McpServerOptions Options = new McpServerOptions
    {
        ServerInfo = new McpServerInfo
        {
            Name = "blastpass-policy-rag",
            Version = "1.0.0",
            Title = "BlastPass Policy Knowledge",
            Description = "Mock policy knowledge base for BlastBox Omega. Search the BlastPass membership, returns, and loyalty policies, or fetch the exact refund rule for a tier. Use this to confirm a member's tier and whether a prorated refund is allowed before running the refund math."
        },
        ProtocolVersion = "2025-11-25",
        Capabilities = new McpCapabilities { Tools = true },
        Instructions = "Call search_policy with the associate's question (and optionally a tier_code of plus/extra/mega) to retrieve the most relevant policy passages. Call get_tier_refund_policy with a tier_code to get the structured refund rule (cooling-off window, proration method, cancellation fee, non-refundable credit) needed to calculate a prorated refund. Call get_markdown_policy with a markdown_type ('promo' or 'clearance') to get the store's pricing markdown guardrails (max discount, margin floor, clearance/manager-flag rules) for merchandising markdown decisions. This is a mock knowledge base with static text."
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

    // ── Static "Knowledge Base" ─────────────────────────────────────────
    //
    // Each passage is a mock policy chunk. tier is plus/extra/mega or "all".
    // Naive search scores passages by how many query words appear in the
    // keywords + text. This is intentionally NOT real RAG — just a demo prop.

    private static readonly JArray Passages = JArray.Parse(@"[
        {
            ""id"":""mem-plus-refund"",
            ""doc"":""BlastPass Plus Membership Terms"",
            ""section"":""Cancellations & Refunds"",
            ""tier"":""plus"",
            ""keywords"":""plus tier1 refund cancel cancellation prorate prorated proration cooling-off cooling off window months remaining price 79.99 fee"",
            ""text"":""BlastPass Plus ($79.99/yr). Cooling-off: cancel within 14 days of activation with under 2 hours streamed for a FULL refund. Otherwise a prorated refund is available for the whole UNUSED months left on the 12-month term (monthly value = 79.99/12 = $6.6658/mo). Plus has NO cancellation fee and no non-refundable credit. Prorated refunds ARE allowed for Plus.""
        },
        {
            ""id"":""mem-extra-refund"",
            ""doc"":""BlastPass Plus Extra Membership Terms"",
            ""section"":""Cancellations & Refunds"",
            ""tier"":""extra"",
            ""keywords"":""extra plus extra tier2 refund cancel cancellation prorate prorated proration cooling-off window months remaining price 129.99 fee 10 vault"",
            ""text"":""BlastPass Plus Extra ($129.99/yr). Cooling-off: cancel within 14 days of activation with under 2 hours streamed for a FULL refund. Otherwise a prorated refund is available for the whole UNUSED months left on the 12-month term (monthly value = 129.99/12 = $10.8325/mo), MINUS a $10 cancellation fee (never below $0). No non-refundable credit. Prorated refunds ARE allowed for Plus Extra.""
        },
        {
            ""id"":""mem-mega-refund"",
            ""doc"":""BlastPass Plus Extra MEGA!!! Membership Terms"",
            ""section"":""Cancellations & Refunds"",
            ""tier"":""mega"",
            ""keywords"":""mega plus extra mega tier3 refund cancel cancellation prorate prorated proration cooling-off window months remaining price 199.99 fee 25 welcome credit 20 non-refundable loot crate"",
            ""text"":""BlastPass Plus Extra MEGA!!! ($199.99/yr). Cooling-off: cancel within 14 days of activation with under 2 hours streamed for a FULL refund. Otherwise a prorated refund is available for the whole UNUSED months left on the 12-month term (monthly value = 199.99/12 = $16.6658/mo), MINUS a $25 cancellation fee AND MINUS the one-time $20 MEGA Welcome Credit, which is non-refundable (never below $0). Prorated refunds ARE allowed for MEGA, but remember to subtract BOTH the $25 fee and the $20 credit.""
        },
        {
            ""id"":""mem-perks"",
            ""doc"":""BlastPass Membership Terms"",
            ""section"":""Tiers & Perks"",
            ""tier"":""all"",
            ""keywords"":""tier tiers perks benefits vault mystery cartridge cloud save blastbuddies co-op which tier difference compare"",
            ""text"":""Tiers: Plus ($79.99) — 4-player co-op, 1 Mystery Cartridge/mo, 25GB cloud saves. Plus Extra ($129.99) — Extra Vault (200+ titles), 100GB saves, 2 cartridges/mo, early demos. Plus Extra MEGA!!! ($199.99) — MEGA Vault (600+ titles incl. day-one exclusives), unlimited saves, 4 cartridges/mo + quarterly Loot Crate, MEGA Lounge, plus a one-time $20 Welcome Credit.""
        },
        {
            ""id"":""returns-windows"",
            ""doc"":""Store Returns & Exchanges Policy"",
            ""section"":""Return Windows"",
            ""tier"":""all"",
            ""keywords"":""return returns exchange window days console accessory game sealed opened receipt eligible eligibility"",
            ""text"":""Return windows (with receipt): Consoles & hardware — 30 days, like-new. Accessories (controllers, headsets) — 30 days. Sealed physical games — 30 days. Opened physical games — 14 days, exchange only for the same title if defective. Digital/redeemed codes — non-returnable once revealed. Day-one MEGA exclusives — NON-RETURNABLE, all sales final.""
        },
        {
            ""id"":""returns-restocking"",
            ""doc"":""Store Returns & Exchanges Policy"",
            ""section"":""Restocking Fees & Store Credit"",
            ""tier"":""all"",
            ""keywords"":""restocking fee store credit refund opened bundle settlement defective warranty percent bonus"",
            ""text"":""Restocking fee: 15% on opened consoles/hardware returned in like-new condition; waived if the item is defective. Defective hardware within 30 days is a free warranty swap, not a return. Refunds go to the original tender; choosing STORE CREDIT instead adds a 10% goodwill bonus on the eligible merchandise total (the bonus does NOT apply to membership-proration refunds or to fees).""
        },
        {
            ""id"":""loyalty-blastpoints"",
            ""doc"":""BlastPoints Loyalty Program Terms"",
            ""section"":""Earning, Promos & Expiry"",
            ""tier"":""all"",
            ""keywords"":""blastpoints loyalty points earn redeem expiry expire promo multiplier triple weekend tier bonus reconcile balance"",
            ""text"":""BlastPoints: earn 10 points per $1 on merchandise. Tier bonus: Extra members +20%, MEGA members +50% on earned points. Promo events (e.g. Triple BLAST Weekend) multiply BASE earn by 3x before the tier bonus. Points from returned items are clawed back. Points expire 90 days after they are earned if the account is inactive. Redemption: 1,000 points = $5 store credit.""
        }
    ]");

    // ── Tool Registration ───────────────────────────────────────────────

    private void RegisterCapabilities(McpRequestHandler handler)
    {
        // 1. search_policy — mock keyword search over the static passages
        handler.AddTool("search_policy",
            "Search the BlastBox Omega policy knowledge base (membership, returns, and loyalty terms) and return the most relevant policy passages. Use this to confirm a member's tier, check whether a prorated refund is allowed, look up return windows, restocking fees, or loyalty rules.",
            schemaConfig: s => s
                .String("query", "What you want to know, in natural language (e.g. 'is a prorated refund allowed for Plus Extra?' or 'return window for an opened console').", required: true)
                .String("tier_code", "Optional tier filter: 'plus', 'extra', or 'mega'. Omit to search all policies.", required: false),
            handler: async (args, ct) =>
            {
                var query = (args.Value<string>("query") ?? "").Trim();
                var tier = (args.Value<string>("tier_code") ?? "").Trim().ToLowerInvariant();
                var terms = Tokenize(query);

                var scored = new List<JObject>();
                foreach (var p in Passages)
                {
                    var pTier = p["tier"]?.ToString() ?? "all";
                    if (tier.Length > 0 && pTier != "all" && pTier != tier)
                        continue;

                    var hay = ((p["keywords"]?.ToString() ?? "") + " " + (p["text"]?.ToString() ?? "")).ToLowerInvariant();
                    int score = 0;
                    foreach (var t in terms)
                        if (hay.Contains(t)) score++;
                    if (tier.Length > 0 && pTier == tier) score += 2; // tier match boost

                    if (score > 0)
                    {
                        scored.Add(new JObject
                        {
                            ["id"] = p["id"],
                            ["document"] = p["doc"],
                            ["section"] = p["section"],
                            ["tier"] = pTier,
                            ["text"] = p["text"],
                            ["score"] = score
                        });
                    }
                }

                var top = scored.OrderByDescending(x => (int)x["score"]).Take(3).ToList();
                if (top.Count == 0)
                {
                    return new JObject
                    {
                        ["query"] = query,
                        ["matches"] = new JArray(),
                        ["note"] = "No matching policy passage. Try mentioning the tier (Plus, Plus Extra, MEGA) or a topic like 'refund', 'return window', or 'BlastPoints'."
                    };
                }

                var results = new JArray();
                foreach (var m in top) results.Add(m);
                return new JObject { ["query"] = query, ["matches"] = results };
            });

        // 2. get_tier_refund_policy — structured refund rule for a tier
        handler.AddTool("get_tier_refund_policy",
            "Return the exact, structured membership refund rule for a BlastPass tier: the cooling-off window, the proration method, the cancellation fee, and any non-refundable credit. Use this once the tier is known so the refund can be calculated precisely.",
            schemaConfig: s => s
                .String("tier_code", "The tier: 'plus' (BlastPass Plus), 'extra' (BlastPass Plus Extra), or 'mega' (BlastPass Plus Extra MEGA!!!).", required: true),
            handler: async (args, ct) =>
            {
                var tier = (args.Value<string>("tier_code") ?? "").Trim().ToLowerInvariant();
                switch (tier)
                {
                    case "plus":
                        return RefundRule("BlastPass Plus", 79.99, 0.00, 0.00);
                    case "extra":
                        return RefundRule("BlastPass Plus Extra", 129.99, 10.00, 0.00);
                    case "mega":
                        return RefundRule("BlastPass Plus Extra MEGA!!!", 199.99, 25.00, 20.00);
                    default:
                        throw new ArgumentException("Unknown tier_code \"" + tier + "\". Use 'plus', 'extra', or 'mega'.");
                }
            });

        // 3. get_markdown_policy
        handler.AddTool("get_markdown_policy",
            "Return the store's pricing markdown guardrails. Pass markdown_type='ask' FIRST to get the clarifying question the manager must answer (promo vs clearance). Once known, pass markdown_type='promo' (temporary promotion: capped discount, margin floor, no clearance) or 'clearance' (discontinuation: no cap/floor, requires a manager flag). Pass the returned guardrails into the markdown-optimizer; never hardcode them.",
            schemaConfig: s => s
                .String("markdown_type", "The markdown type: 'promo' (temporary promotion), 'clearance' (discontinuation), or 'ask' to receive the clarifying question to put to the manager.", required: true),
            handler: async (args, ct) =>
            {
                var type = (args.Value<string>("markdown_type") ?? "").Trim().ToLowerInvariant();
                switch (type)
                {
                    case "":
                    case "ask":
                        return new JObject
                        {
                            ["needs_clarification"] = true,
                            ["question"] = "Are these markdowns a temporary end-of-quarter PROMOTION, or a CLEARANCE because we're discontinuing the items? Promotions are capped and must stay above the margin floor; clearance has no floor but needs manager approval.",
                            ["options"] = new JArray("promo", "clearance"),
                            ["rule"] = "The markdown type determines the discount cap and margin floor, so confirm it with the manager before recommending any discounts."
                        };
                    case "promo":
                        return new JObject
                        {
                            ["markdown_type"] = "promo",
                            ["max_discount_pct"] = 30,
                            ["margin_floor_pct"] = 15,
                            ["min_useful_promo_discount_pct"] = 10,
                            ["clearance_allowed"] = false,
                            ["requires_manager_flag"] = false,
                            ["rule"] = "Temporary promotion: discount each item by up to 30% off its current price, but never below a 15% gross margin. If the deepest discount allowed by the margin floor is under min_useful_promo_discount_pct (10%), the item cannot be meaningfully promoted and should be flagged for clearance instead."
                        };
                    case "clearance":
                        return new JObject
                        {
                            ["markdown_type"] = "clearance",
                            ["max_discount_pct"] = null,
                            ["margin_floor_pct"] = null,
                            ["min_useful_promo_discount_pct"] = null,
                            ["clearance_allowed"] = true,
                            ["requires_manager_flag"] = true,
                            ["rule"] = "Clearance / discontinuation: no discount cap and no margin floor (may sell below cost to clear), but every clearance markdown requires explicit manager approval (manager flag)."
                        };
                    default:
                        return new JObject
                        {
                            ["needs_clarification"] = true,
                            ["question"] = "I didn't recognize that markdown type. Is this a temporary PROMOTION or a CLEARANCE (discontinuation)?",
                            ["options"] = new JArray("promo", "clearance")
                        };
                }
            });
    }

    private static JObject RefundRule(string name, double annual, double fee, double credit)
    {
        return new JObject
        {
            ["tier"] = name,
            ["annual_price"] = annual,
            ["monthly_value"] = Math.Round(annual / 12.0, 4),
            ["term_months"] = 12,
            ["cooling_off_days"] = 14,
            ["cooling_off_max_hours_streamed"] = 2,
            ["cooling_off_rule"] = "If cancelled within 14 days of activation AND under 2 hours streamed, issue a FULL refund of the annual price (no fee).",
            ["proration_rule"] = "Otherwise refund = whole UNUSED months remaining x (annual_price / 12), then subtract the cancellation fee and any non-refundable credit; never below $0.",
            ["cancellation_fee"] = fee,
            ["nonrefundable_credit"] = credit,
            ["prorated_refund_allowed"] = true
        };
    }

    // Naive tokenizer: lowercase, split on non-alphanumeric, drop short/stop words.
    private static List<string> Tokenize(string s)
    {
        var stop = new HashSet<string> { "the","a","an","is","are","for","to","of","and","or","my","i","do","does","can","what","whats","how","on","in","it","be","with","please" };
        var sb = new StringBuilder();
        foreach (var ch in (s ?? "").ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        var tokens = new List<string>();
        foreach (var w in sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            if (w.Length >= 3 && !stop.Contains(w) && !tokens.Contains(w))
                tokens.Add(w);
        return tokens;
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

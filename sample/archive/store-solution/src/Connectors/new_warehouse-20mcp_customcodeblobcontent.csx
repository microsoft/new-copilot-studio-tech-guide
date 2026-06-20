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
// ║  Warehouse MCP Connector (inline)                                           ║
// ║                                                                            ║
// ║  7 tools with static mock data — no external server needed.                ║
// ║  Based on Power MCP Template v2.1 by Troy Taylor.                          ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

public class Script : ScriptBase
{


    private static readonly McpServerOptions Options = new McpServerOptions
    {
        ServerInfo = new McpServerInfo
        {
            Name = "warehouse-fulfillment",
            Version = "1.0.0",
            Title = "Warehouse & Fulfillment",
            Description = "Warehouse inventory and fulfillment tools for checking stock, tracking fulfillment pipeline stages, finding product alternatives, and looking up restock dates."
        },
        ProtocolVersion = "2025-11-25",
        Capabilities = new McpCapabilities { Tools = true },
        Instructions = "Use check_stock to look up inventory for a product SKU. If out of stock, use get_restock_date for restock info and find_alternatives for similar products. Use get_fulfillment_status with an order_id to check where an order is in the warehouse pipeline. Use check_game_compatibility to find which BlastBox Omega model a game requires. Use get_inventory_aging to see how long a product's stock has been sitting and whether more is inbound, to inform markdown/clearance decisions. Use get_console_exclusives to list the MEGA-only AAA titles (with an associate upsell pitch) when helping a customer decide between the base console and the MEGA Edition."
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

    private static readonly JArray Stock = JArray.Parse(@"[
        {""sku"":""SKU-OMEGA"",""name"":""BlastBox Omega Console"",""category"":""console"",""price"":399.99,""quantity"":0,""warehouse"":""Chicago-IL1"",""aisle"":""E-20"",""availableForShipping"":false},
        {""sku"":""SKU-OMEGA-MEGA"",""name"":""BlastBox Omega MEGA Edition"",""category"":""console"",""price"":499.99,""quantity"":5,""warehouse"":""Seattle-WA1"",""aisle"":""E-21"",""availableForShipping"":true},
        {""sku"":""SKU-APEX5"",""name"":""Apex NoiseGuard 5 Headphones"",""category"":""electronics"",""quantity"":3,""warehouse"":""Seattle-WA1"",""aisle"":""E-14"",""availableForShipping"":true},
        {""sku"":""SKU-USBC3"",""name"":""USB-C Charging Cable (3-pack)"",""category"":""electronics"",""quantity"":142,""warehouse"":""Seattle-WA1"",""aisle"":""A-02"",""availableForShipping"":true},
        {""sku"":""SKU-LUMI"",""name"":""LumiRead E-Reader (16GB)"",""category"":""electronics"",""quantity"":0,""warehouse"":""Chicago-IL1"",""aisle"":""E-08"",""availableForShipping"":false},
        {""sku"":""SKU-PULSE"",""name"":""PulseWave Pro Earbuds"",""category"":""electronics"",""quantity"":17,""warehouse"":""Seattle-WA1"",""aisle"":""E-12"",""availableForShipping"":true},
        {""sku"":""SKU-VORTEX"",""name"":""Vortex Gaming Console OLED"",""category"":""electronics"",""quantity"":0,""warehouse"":""Chicago-IL1"",""aisle"":""E-22"",""availableForShipping"":false},
        {""sku"":""SKU-REALM"",""name"":""Realm of Legends: The Lost Crown"",""category"":""electronics"",""quantity"":24,""warehouse"":""Chicago-IL1"",""aisle"":""G-05"",""availableForShipping"":true},
        {""sku"":""SKU-APEX4"",""name"":""Apex NoiseGuard 4 Headphones (Previous Gen)"",""category"":""electronics"",""quantity"":8,""warehouse"":""Seattle-WA1"",""aisle"":""E-14"",""availableForShipping"":true},
        {""sku"":""SKU-CLAR7"",""name"":""Clarity NC700 Headphones"",""category"":""electronics"",""quantity"":6,""warehouse"":""Seattle-WA1"",""aisle"":""E-15"",""availableForShipping"":true},
        {""sku"":""SKU-PWMAX"",""name"":""PulseWave Max Headphones - Space Gray"",""category"":""electronics"",""quantity"":2,""warehouse"":""Seattle-WA1"",""aisle"":""E-13"",""availableForShipping"":true},
        {""sku"":""SKU-HOODIE"",""name"":""TrailMark Fleece Hoodie - Black (L)"",""category"":""clothing"",""quantity"":4,""warehouse"":""Dallas-TX1"",""aisle"":""C-07"",""availableForShipping"":true},
        {""sku"":""SKU-HOODIE-XL"",""name"":""TrailMark Fleece Hoodie - Black (XL)"",""category"":""clothing"",""quantity"":7,""warehouse"":""Dallas-TX1"",""aisle"":""C-07"",""availableForShipping"":true},
        {""sku"":""SKU-HOODIE-GRY"",""name"":""TrailMark Fleece Hoodie - Grey (L)"",""category"":""clothing"",""quantity"":2,""warehouse"":""Dallas-TX1"",""aisle"":""C-07"",""availableForShipping"":true},
        {""sku"":""SKU-JOGGER"",""name"":""TrailMark Sport Joggers - Grey (L)"",""category"":""clothing"",""quantity"":11,""warehouse"":""Dallas-TX1"",""aisle"":""C-09"",""availableForShipping"":true},
        {""sku"":""SKU-STAR2"",""name"":""Starfall: Part Two (4K Blu-ray)"",""category"":""media"",""quantity"":9,""warehouse"":""Chicago-IL1"",""aisle"":""M-03"",""availableForShipping"":true},
        {""sku"":""SKU-COMFT"",""name"":""ComfortEdge Pro - Matte Black"",""category"":""furniture"",""quantity"":1,""warehouse"":""Seattle-WA1"",""aisle"":""F-01"",""availableForShipping"":true},
        {""sku"":""SKU-COMFT-W"",""name"":""ComfortEdge Pro - White"",""category"":""furniture"",""quantity"":3,""warehouse"":""Seattle-WA1"",""aisle"":""F-01"",""availableForShipping"":true},
        {""sku"":""SKU-DESKMAT"",""name"":""Felt Desk Mat - Dark Grey (90x40cm)"",""category"":""furniture"",""quantity"":30,""warehouse"":""Seattle-WA1"",""aisle"":""F-04"",""availableForShipping"":true}
    ]");

    private static readonly JObject Fulfillment = JObject.Parse(@"{
        ""ORD-10422"":{""order_id"":""ORD-10422"",""warehouse"":""Chicago-IL1"",""assigned_worker"":""Mike Torres"",""current_stage"":""received"",""estimated_ship_date"":""2026-04-23"",""notes"":""LumiRead E-Reader — awaiting restock. Item reserved from incoming shipment."",""pipeline"":[{""stage"":""received"",""completed_at"":""2026-04-15T09:30:00Z""}]},
        ""ORD-10460"":{""order_id"":""ORD-10460"",""warehouse"":""Chicago-IL1"",""assigned_worker"":""Lisa Park"",""current_stage"":""picked"",""estimated_ship_date"":""2026-04-21"",""notes"":""Vortex Console on backorder, Realm of Legends picked, holding for bundle ship."",""pipeline"":[{""stage"":""received"",""completed_at"":""2026-04-18T10:00:00Z""},{""stage"":""picked"",""completed_at"":""2026-04-19T14:20:00Z""}]},
        ""ORD-10421"":{""order_id"":""ORD-10421"",""warehouse"":""Seattle-WA1"",""assigned_worker"":""Carlos Mendez"",""current_stage"":""handed_to_carrier"",""estimated_ship_date"":""2026-04-13"",""notes"":""Shipped via SwiftShip. Two items in single box."",""pipeline"":[{""stage"":""received"",""completed_at"":""2026-04-11T08:00:00Z""},{""stage"":""picked"",""completed_at"":""2026-04-11T11:30:00Z""},{""stage"":""packed"",""completed_at"":""2026-04-12T09:15:00Z""},{""stage"":""labeled"",""completed_at"":""2026-04-12T10:00:00Z""},{""stage"":""handed_to_carrier"",""completed_at"":""2026-04-13T08:45:00Z""}]},
        ""ORD-10455"":{""order_id"":""ORD-10455"",""warehouse"":""Dallas-TX1"",""assigned_worker"":""Rachel Kim"",""current_stage"":""handed_to_carrier"",""estimated_ship_date"":""2026-04-14"",""notes"":""Shipped via PrimeFreight. Hoodie + joggers in one package."",""pipeline"":[{""stage"":""received"",""completed_at"":""2026-04-12T11:00:00Z""},{""stage"":""picked"",""completed_at"":""2026-04-13T09:00:00Z""},{""stage"":""packed"",""completed_at"":""2026-04-13T14:30:00Z""},{""stage"":""labeled"",""completed_at"":""2026-04-14T08:00:00Z""},{""stage"":""handed_to_carrier"",""completed_at"":""2026-04-14T15:00:00Z""}]}
    }");

    private static readonly JArray Restock = JArray.Parse(@"[
        {""sku"":""SKU-OMEGA"",""name"":""BlastBox Omega Console"",""current_quantity"":0,""next_shipment_date"":""2026-06-11"",""expected_quantity"":40,""supplier"":""BlastBox Manufacturing"",""notes"":""Standard replenishment — next batch arrives in about 4 days. MEGA Edition is in stock now as an upgrade alternative.""},
        {""sku"":""SKU-LUMI"",""name"":""LumiRead E-Reader (16GB)"",""current_quantity"":0,""next_shipment_date"":""2026-04-22"",""expected_quantity"":50,""supplier"":""LumiRead Distribution Co."",""notes"":""Delayed from original April 18 date. Supplier confirmed new ETA.""},
        {""sku"":""SKU-VORTEX"",""name"":""Vortex Gaming Console OLED"",""current_quantity"":0,""next_shipment_date"":""2026-04-28"",""expected_quantity"":30,""supplier"":""Vortex Games Inc."",""notes"":""High demand — limited allocation. Next batch after this is mid-May.""},
        {""sku"":""SKU-APEX5"",""name"":""Apex NoiseGuard 5 Headphones"",""current_quantity"":3,""next_shipment_date"":""2026-05-05"",""expected_quantity"":20,""supplier"":""Apex Audio Corp."",""notes"":""Regular replenishment cycle. Current stock sufficient for 1-2 weeks.""},
        {""sku"":""SKU-COMFT"",""name"":""ComfortEdge Pro - Matte Black"",""current_quantity"":1,""next_shipment_date"":""2026-04-30"",""expected_quantity"":10,""supplier"":""ComfortEdge Furniture Co."",""notes"":""Low stock alert triggered. Express shipment arranged.""}
    ]");

    private static readonly JArray Games = JArray.Parse(@"[
        {""title"":""MEGA Lizards from Outer Space"",""required_model"":""BlastBox Omega MEGA Edition"",""required_sku"":""SKU-OMEGA-MEGA"",""runs_on_base"":false,""note"":""AAA title — requires the MEGA Edition GPU. Will NOT run on the base BlastBox Omega.""},
        {""title"":""Galactic Tax Evader VII: Audit Protocol"",""required_model"":""BlastBox Omega MEGA Edition"",""required_sku"":""SKU-OMEGA-MEGA"",""runs_on_base"":false,""note"":""AAA open-world heist sim — the real-time audit physics only run on the MEGA Edition co-processor.""},
        {""title"":""Mecha-Granny: Knitpocalypse"",""required_model"":""BlastBox Omega MEGA Edition"",""required_sku"":""SKU-OMEGA-MEGA"",""runs_on_base"":false,""note"":""AAA action roguelite — the 4K yarn engine requires the MEGA Edition. Will NOT run on the base console.""},
        {""title"":""Realm of Legends: The Lost Crown"",""required_model"":""BlastBox Omega Console"",""required_sku"":""SKU-OMEGA"",""runs_on_base"":true,""note"":""Runs on any BlastBox Omega model, including the base console.""},
        {""title"":""Galaxy Smash"",""required_model"":""BlastBox Omega Console"",""required_sku"":""SKU-OMEGA"",""runs_on_base"":true,""note"":""Runs on any BlastBox Omega model.""}
    ]");

    // Exclusive AAA titles per console model, with a ready-to-say associate upsell pitch.
    private static readonly JObject Exclusives = JObject.Parse(@"{
        ""SKU-OMEGA-MEGA"":{
            ""model"":""BlastBox Omega MEGA Edition"",
            ""sku"":""SKU-OMEGA-MEGA"",
            ""tagline"":""Three AAA exclusives the base console literally can't load."",
            ""titles"":[
                {""title"":""MEGA Lizards from Outer Space"",""genre"":""Co-op chaos shooter"",""pitch"":""Picture two hundred neon space-lizards on screen at once and a co-op buddy screaming next to you. It's the #1 couch-chaos shooter of the year — and it physically will not run on the base console.""},
                {""title"":""Galactic Tax Evader VII: Audit Protocol"",""genre"":""Open-world heist sim"",""pitch"":""You're an interstellar accountant on the run, dodging audits in real time across a living open galaxy. Critics call it 'Grand Theft Spreadsheet.' MEGA-exclusive, because the audit physics melt anything smaller.""},
                {""title"":""Mecha-Granny: Knitpocalypse"",""genre"":""Action roguelite"",""pitch"":""A sweet old lady in a battle-mech knits the apocalypse back together, one 4K scarf at a time. It's hilarious, it's brutal, and the yarn engine only spins up on MEGA.""}
            ]
        },
        ""SKU-OMEGA"":{
            ""model"":""BlastBox Omega Console"",
            ""sku"":""SKU-OMEGA"",
            ""tagline"":""Plays the full shared BlastBox library, but none of the MEGA-only AAA exclusives."",
            ""titles"":[]
        }
    }");

    // Inventory aging / inbound status for merchandising markdown decisions.
    private static readonly JArray Aging = JArray.Parse(@"[
        {""sku"":""SKU-OMEGA-MEGA"",""name"":""BlastBox Omega MEGA Edition"",""weeks_in_stock"":3,""received_date"":""2026-05-11"",""inbound_po"":true,""next_inbound_date"":""2026-06-15"",""aging_bucket"":""fresh"",""note"":""Hot seller. Replenishment PO inbound.""},
        {""sku"":""SKU-MEGALIZARDS"",""name"":""MEGA Lizards from Outer Space"",""weeks_in_stock"":4,""received_date"":""2026-05-04"",""inbound_po"":true,""next_inbound_date"":""2026-06-12"",""aging_bucket"":""fresh"",""note"":""AAA title driving MEGA Edition attach. Reorder inbound.""},
        {""sku"":""SKU-PULSE-CTRL"",""name"":""PulseGrip Pro Controller"",""weeks_in_stock"":5,""received_date"":""2026-04-27"",""inbound_po"":true,""next_inbound_date"":""2026-06-20"",""aging_bucket"":""fresh"",""note"":""Steady accessory attach.""},
        {""sku"":""SKU-OMEGA-CORE"",""name"":""BlastBox Omega Core (1st-gen)"",""weeks_in_stock"":16,""received_date"":""2026-02-16"",""inbound_po"":false,""next_inbound_date"":null,""aging_bucket"":""aging"",""note"":""Being phased out by the MEGA Edition. No further inbound — candidate for discontinuation/clearance.""},
        {""sku"":""SKU-RETRO-CADET"",""name"":""BlastBox Cadet Bundle"",""weeks_in_stock"":22,""received_date"":""2026-01-05"",""inbound_po"":false,""next_inbound_date"":null,""aging_bucket"":""stale"",""note"":""Last-gen bundle. No inbound. Strong markdown candidate.""},
        {""sku"":""SKU-GALAXY-SMASH"",""name"":""Galaxy Smash"",""weeks_in_stock"":14,""received_date"":""2026-03-02"",""inbound_po"":false,""next_inbound_date"":null,""aging_bucket"":""aging"",""note"":""Overstocked at launch. No inbound. Markdown candidate.""},
        {""sku"":""SKU-VR-GOGGLES"",""name"":""OmegaVision VR Headset"",""weeks_in_stock"":18,""received_date"":""2026-02-02"",""inbound_po"":false,""next_inbound_date"":null,""aging_bucket"":""stale"",""note"":""Slow launch. No inbound. Strong markdown/clearance candidate.""}
    ]");

    // ── Tool Registration ───────────────────────────────────────────────

    private void RegisterCapabilities(McpRequestHandler handler)
    {
        // 1. check_stock
        handler.AddTool("check_stock",
            "Check warehouse inventory for a product by SKU. Returns quantity on hand, warehouse location, aisle, and whether the item is available for shipping. If quantity is 0, use get_restock_date for restock info.",
            schemaConfig: s => s.String("sku", "Product SKU (e.g. 'SKU-APEX5'). Use SKUs from order line items.", required: true),
            handler: async (args, ct) =>
            {
                var sku = args.Value<string>("sku") ?? "";
                foreach (var item in Stock)
                {
                    if (item["sku"]?.ToString() == sku)
                    {
                        var qty = item["quantity"]?.Value<int>() ?? 0;
                        return new JObject
                        {
                            ["sku"] = item["sku"],
                            ["name"] = item["name"],
                            ["category"] = item["category"],
                            ["price"] = item["price"],
                            ["quantity_on_hand"] = qty,
                            ["warehouse"] = item["warehouse"],
                            ["aisle"] = item["aisle"],
                            ["available_for_shipping"] = item["availableForShipping"],
                            ["status"] = qty == 0 ? "OUT_OF_STOCK" : qty <= 3 ? "LOW_STOCK" : "IN_STOCK"
                        };
                    }
                }
                throw new ArgumentException($"SKU \"{sku}\" not found in warehouse inventory.");
            });

        // 2. get_fulfillment_status
        handler.AddTool("get_fulfillment_status",
            "Get the fulfillment pipeline status for an order. Shows which warehouse stage the order is at (received → picked → packed → labeled → handed_to_carrier), assigned worker, and estimated ship date.",
            schemaConfig: s => s.String("order_id", "Order ID (e.g. 'ORD-10422') from the order management system", required: true),
            handler: async (args, ct) =>
            {
                var orderId = args.Value<string>("order_id") ?? "";
                if (Fulfillment[orderId] != null)
                    return JObject.Parse(Fulfillment[orderId].ToString());
                throw new ArgumentException($"No fulfillment record found for order \"{orderId}\". The order may not have entered the warehouse yet.");
            });

        // 3. find_alternatives
        handler.AddTool("find_alternatives",
            "Find alternative products similar to a given SKU. Returns items in the same category that are in stock. Useful when a customer wants a different size, color, or comparable product, or when an item is out of stock.",
            schemaConfig: s => s.String("sku", "Product SKU to find alternatives for (e.g. 'SKU-HOODIE')", required: true),
            handler: async (args, ct) =>
            {
                var sku = args.Value<string>("sku") ?? "";
                JToken original = null;
                foreach (var item in Stock)
                {
                    if (item["sku"]?.ToString() == sku) { original = item; break; }
                }
                if (original == null)
                    throw new ArgumentException($"SKU \"{sku}\" not found in inventory.");

                var category = original["category"]?.ToString();
                var alternatives = new JArray();
                foreach (var item in Stock)
                {
                    if (item["category"]?.ToString() == category && item["sku"]?.ToString() != sku && (item["quantity"]?.Value<int>() ?? 0) > 0)
                    {
                        alternatives.Add(new JObject
                        {
                            ["sku"] = item["sku"],
                            ["name"] = item["name"],
                            ["price"] = item["price"],
                            ["quantity_available"] = item["quantity"],
                            ["warehouse"] = item["warehouse"],
                            ["available_for_shipping"] = item["availableForShipping"]
                        });
                    }
                }

                return new JObject
                {
                    ["original"] = new JObject { ["sku"] = original["sku"], ["name"] = original["name"], ["category"] = category },
                    ["alternatives"] = alternatives
                };
            });

        // 4. get_restock_date
        handler.AddTool("get_restock_date",
            "Get the next restock date and supplier info for a product. Use this when check_stock shows an item is out of stock or low stock. Returns expected delivery date, quantity, and supplier details.",
            schemaConfig: s => s.String("sku", "Product SKU (e.g. 'SKU-LUMI'). Typically used after check_stock shows low/no stock.", required: true),
            handler: async (args, ct) =>
            {
                var sku = args.Value<string>("sku") ?? "";
                foreach (var item in Restock)
                {
                    if (item["sku"]?.ToString() == sku)
                        return JObject.Parse(item.ToString());
                }
                return new JObject { ["message"] = $"No restock schedule found for SKU \"{sku}\". The item may be regularly stocked or discontinued." };
            });

        // 5. check_game_compatibility
        handler.AddTool("check_game_compatibility",
            "Check which BlastBox Omega console model a game requires. Use when a customer asks whether a game will run on their console, or when deciding between the base console and the MEGA Edition. Returns the required model/SKU and whether it runs on the base console.",
            schemaConfig: s => s.String("game_title", "The game title (e.g. 'MEGA Lizards from Outer Space'). Case-insensitive, partial match allowed.", required: true),
            handler: async (args, ct) =>
            {
                var q = (args.Value<string>("game_title") ?? "").Trim().ToLowerInvariant();
                foreach (var g in Games)
                {
                    var title = (g["title"]?.ToString() ?? "").ToLowerInvariant();
                    if (title == q || (q.Length > 0 && title.Contains(q)))
                        return JObject.Parse(g.ToString());
                }
                return new JObject { ["message"] = $"No compatibility record found for game \"{args.Value<string>("game_title")}\"." };
            });

        // 6. get_inventory_aging
        handler.AddTool("get_inventory_aging",
            "Return inventory aging and inbound-replenishment status for a product SKU: how many weeks the stock has been sitting, its received date, whether there is an inbound purchase order, and an aging bucket (fresh / aging / stale). Use this to judge whether a slow-selling item is a markdown or clearance candidate. Omit sku to return aging for all tracked products.",
            schemaConfig: s => s.String("sku", "Optional. Product SKU (e.g. 'SKU-VR-GOGGLES'). Omit to return all tracked products."),
            handler: async (args, ct) =>
            {
                var sku = (args.Value<string>("sku") ?? "").Trim();
                if (sku.Length == 0)
                    return new JObject { ["count"] = Aging.Count, ["items"] = JArray.Parse(Aging.ToString()) };
                foreach (var item in Aging)
                {
                    if (string.Equals(item["sku"]?.ToString(), sku, StringComparison.OrdinalIgnoreCase))
                        return JObject.Parse(item.ToString());
                }
                return new JObject { ["message"] = $"No aging record found for SKU \"{sku}\"." };
            });

        // 7. get_console_exclusives
        handler.AddTool("get_console_exclusives",
            "Return the AAA game titles that run ONLY on a given BlastBox Omega console model, each with a ready-to-say associate upsell pitch. Use this to help an associate upsell a customer to the MEGA Edition: it surfaces the marquee titles the base console cannot run plus a short, attractive blurb to read to the customer. Accepts a SKU (e.g. 'SKU-OMEGA-MEGA') or a model name (e.g. 'mega', 'base').",
            schemaConfig: s => s.String("model", "Console SKU or model name. Use 'SKU-OMEGA-MEGA' or 'mega' for the MEGA Edition; 'SKU-OMEGA' or 'base' for the base console.", required: true),
            handler: async (args, ct) =>
            {
                var q = (args.Value<string>("model") ?? "").Trim().ToLowerInvariant();
                string key = null;
                if (q.Contains("mega")) key = "SKU-OMEGA-MEGA";
                else if (q == "base" || q == "sku-omega" || q.Contains("base")) key = "SKU-OMEGA";
                else if (q == "sku-omega-mega") key = "SKU-OMEGA-MEGA";
                if (key != null && Exclusives[key] != null)
                    return JObject.Parse(Exclusives[key].ToString());
                return new JObject { ["message"] = $"No console-exclusive list found for model \"{args.Value<string>("model")}\". Try 'mega' or 'base'." };
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

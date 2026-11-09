using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DocxUpdater;

class Program
{
    static readonly XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace s = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    static void Main(string[] args)
    {
        string baseDir = @"d:\SWP391-G5-Test\info";
        string srsDocxPath = Path.Combine(baseDir, "Report 1 - SRS.docx");
        string sdsDocxPath = Path.Combine(baseDir, "Report 3 - SDS.docx");
        string trackingXlsxPath = Path.Combine(baseDir, "Tracking (1).xlsx");
        string srsMdPath = Path.Combine(baseDir, "SRS.md");
        string sdsMdPath = Path.Combine(baseDir, "SDS.md");

        Console.WriteLine("Updating Tracking (1).xlsx...");
        UpdateTrackingXlsx(trackingXlsxPath);

        Console.WriteLine("Updating Report 1 - SRS.docx from baseline...");
        string origSrsPath = Path.Combine(baseDir, "orig_SRS.docx");
        if (File.Exists(origSrsPath))
        {
            File.Copy(origSrsPath, srsDocxPath, overwrite: true);
        }
        UpdateSrsDocx(srsDocxPath);
        File.Copy(srsDocxPath, Path.Combine(baseDir, "SRS.docx"), overwrite: true);

        Console.WriteLine("Updating Report 3 - SDS.docx...");
        UpdateSdsDocx(sdsDocxPath);
        File.Copy(sdsDocxPath, Path.Combine(baseDir, "SDS.docx"), overwrite: true);

        Console.WriteLine("Updating SRS.md...");
        UpdateSrsMarkdown(srsMdPath);

        Console.WriteLine("Updating SDS.md...");
        UpdateSdsMarkdown(sdsMdPath);

        Console.WriteLine("ALL FILES UPDATED SUCCESSFULLY WITH FULL TRACKING ALIGNMENT!");
    }

    #region Helper OpenXML Methods

    static XElement CreateParagraph(string text, string? style = null, bool bold = false)
    {
        var p = new XElement(w + "p");
        if (!string.IsNullOrEmpty(style))
        {
            p.Add(new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", style))));
        }

        if (!string.IsNullOrEmpty(text))
        {
            var r = new XElement(w + "r");
            if (bold)
            {
                r.Add(new XElement(w + "rPr", new XElement(w + "b")));
            }
            r.Add(new XElement(w + "t", text));
            p.Add(r);
        }
        return p;
    }

    static XElement CreateTableCell(string text, string width, bool isHeader = false)
    {
        var tcPr = new XElement(w + "tcPr",
            new XElement(w + "tcW", new XAttribute(w + "w", width), new XAttribute(w + "type", "dxa")),
            new XElement(w + "tcBorders",
                new XElement(w + "top", new XAttribute(w + "val", "single"), new XAttribute(w + "sz", "4"), new XAttribute(w + "space", "0"), new XAttribute(w + "color", "CCCCCC")),
                new XElement(w + "bottom", new XAttribute(w + "val", "single"), new XAttribute(w + "sz", "4"), new XAttribute(w + "space", "0"), new XAttribute(w + "color", "CCCCCC")),
                new XElement(w + "left", new XAttribute(w + "val", "single"), new XAttribute(w + "sz", "4"), new XAttribute(w + "space", "0"), new XAttribute(w + "color", "CCCCCC")),
                new XElement(w + "right", new XAttribute(w + "val", "single"), new XAttribute(w + "sz", "4"), new XAttribute(w + "space", "0"), new XAttribute(w + "color", "CCCCCC"))
            )
        );

        var p = new XElement(w + "p");
        var r = new XElement(w + "r");
        if (isHeader)
        {
            r.Add(new XElement(w + "rPr", new XElement(w + "b")));
        }
        r.Add(new XElement(w + "t", text ?? ""));
        p.Add(r);

        return new XElement(w + "tc", tcPr, p);
    }

    static XElement CreateTableRow(string[] cellTexts, string[] widths, bool isHeader = false)
    {
        var tr = new XElement(w + "tr");
        for (int i = 0; i < cellTexts.Length; i++)
        {
            string width = i < widths.Length ? widths[i] : "2000";
            tr.Add(CreateTableCell(cellTexts[i], width, isHeader));
        }
        return tr;
    }

    static XElement CreateTable(List<string[]> rows, string[] widths)
    {
        var tbl = new XElement(w + "tbl",
            new XElement(w + "tblPr",
                new XElement(w + "tblW", new XAttribute(w + "w", "9200"), new XAttribute(w + "type", "dxa")),
                new XElement(w + "jc", new XAttribute(w + "val", "left")),
                new XElement(w + "tblBorders",
                    new XElement(w + "top", new XAttribute(w + "val", "single"), new XAttribute(w + "sz", "4"), new XAttribute(w + "space", "0"), new XAttribute(w + "color", "CCCCCC")),
                    new XElement(w + "bottom", new XAttribute(w + "val", "single"), new XAttribute(w + "sz", "4"), new XAttribute(w + "space", "0"), new XAttribute(w + "color", "CCCCCC")),
                    new XElement(w + "insideH", new XAttribute(w + "val", "single"), new XAttribute(w + "sz", "4"), new XAttribute(w + "space", "0"), new XAttribute(w + "color", "E5E5E5"))
                )
            )
        );

        for (int r = 0; r < rows.Count; r++)
        {
            tbl.Add(CreateTableRow(rows[r], widths, isHeader: r == 0));
        }

        return tbl;
    }

    static XElement CreateUCTable(string ucId, string ucName, string actor, string desc, string trigger, string pre, string post, string normalFlow, string altFlow, string exc, string priority = "High")
    {
        var rows = new List<string[]>
        {
            new[] { "UC Name:", $"{ucId}: {ucName}" },
            new[] { "Primary Actor:", actor },
            new[] { "Description:", desc },
            new[] { "Trigger:", trigger },
            new[] { "Precondition:", pre },
            new[] { "Postconditions:", post },
            new[] { "Normal Flow:", normalFlow },
            new[] { "Alternative Flows:", altFlow },
            new[] { "Exceptions:", exc },
            new[] { "Priority:", priority }
        };

        var tbl = new XElement(w + "tbl",
            new XElement(w + "tblPr",
                new XElement(w + "tblW", new XAttribute(w + "w", "9200"), new XAttribute(w + "type", "dxa")),
                new XElement(w + "jc", new XAttribute(w + "val", "left"))
            )
        );

        string[] widths = new[] { "2200", "7000" };
        foreach (var row in rows)
        {
            tbl.Add(CreateTableRow(row, widths, isHeader: false));
        }
        return tbl;
    }

    #endregion

    #region Update Tracking XLSX

    static void UpdateTrackingXlsx(string xlsxPath)
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "track_upd_" + Guid.NewGuid().ToString("N"));
        ZipFile.ExtractToDirectory(xlsxPath, tempFolder);
        string sheet2Path = Path.Combine(tempFolder, "xl", "worksheets", "sheet2.xml");

        var doc = XDocument.Load(sheet2Path);
        var sheetData = doc.Root?.Element(s + "sheetData");
        if (sheetData == null) throw new InvalidOperationException("sheetData not found in Tracking sheet2.xml");

        // Map row numbers in Sheet 2 to update status = Done and Notes
        var updates = new Dictionary<int, (string Status, string? Note)>
        {
            { 10, ("Done", null) },                                                                // UC-13: Voucher Management (TinDLB)
            { 11, ("Done", null) },                                                                // UC-10: Cashier Dashboard (ThaiTQ)
            { 12, ("Done", null) },                                                                // UC-11: POS / Counter Order (ThaiTQ)
            { 20, ("Done", null) },                                                                // UC-24: Admin Stock Alert Notification (TinDLB)
            { 21, ("Done", null) },                                                                // UC-35: Staff Account Management (TinDLB)
            { 22, ("Done", null) },                                                                // UC-40: Order Monitoring Admin (TinDLB)
            { 23, ("Done", null) },                                                                // UC-39: Admin Dashboard (TinDLB)
            { 24, ("Done", null) },                                                                // UC-37: Production Task Management (TinDLB)
            { 25, ("Done", null) },                                                                // UC-38: Production Notifications (TinDLB)
            { 26, ("Done", "Kèm chức năng tải/xuất báo cáo CSV của Admin") },                       // UC-30: Revenue Report (TinDLB)
            { 27, ("Done", "Tự động tạo, xem, in và tải hóa đơn HTML") },                          // UC-12: Invoice Generation (ThaiTQ)
            { 28, ("Done", null) },                                                                // UC-14: Order Management Online List & Filter (ThaiTQ)
            { 29, ("Done", "Kèm tự động thông báo cho khách hàng khi cập nhật trạng thái đơn") },    // UC-15: Order Status Update (ThaiTQ)
            { 30, ("Done", "Kèm tự động thông báo cho khách hàng khi phân công người giao hàng") }  // UC-16: Delivery & Shipper Management (ThaiTQ)
        };

        foreach (var row in sheetData.Elements(s + "row"))
        {
            string? rowIdxStr = (string?)row.Attribute("r");
            if (int.TryParse(rowIdxStr, out int rowIdx) && updates.TryGetValue(rowIdx, out var upd))
            {
                // Update cell L{rowIdx} (Status)
                string cellLRef = $"L{rowIdx}";
                var cellL = row.Elements(s + "c").FirstOrDefault(c => (string?)c.Attribute("r") == cellLRef);
                if (cellL == null)
                {
                    cellL = new XElement(s + "c", new XAttribute("r", cellLRef));
                    row.Add(cellL);
                }
                cellL.Attribute("t")?.Remove();
                cellL.SetAttributeValue("t", "inlineStr");
                cellL.RemoveNodes();
                cellL.Add(new XElement(s + "is", new XElement(s + "t", upd.Status)));

                // Update cell M{rowIdx} (Notes / Updates) if note provided
                if (!string.IsNullOrEmpty(upd.Note))
                {
                    string cellMRef = $"M{rowIdx}";
                    var cellM = row.Elements(s + "c").FirstOrDefault(c => (string?)c.Attribute("r") == cellMRef);
                    if (cellM == null)
                    {
                        cellM = new XElement(s + "c", new XAttribute("r", cellMRef));
                        row.Add(cellM);
                    }
                    cellM.Attribute("t")?.Remove();
                    cellM.SetAttributeValue("t", "inlineStr");
                    cellM.RemoveNodes();
                    cellM.Add(new XElement(s + "is", new XElement(s + "t", upd.Note)));
                }
            }
        }

        doc.Save(sheet2Path);
        string tempRepack = Path.Combine(Path.GetTempPath(), "track_repack_" + Guid.NewGuid().ToString("N") + ".xlsx");
        ZipFile.CreateFromDirectory(tempFolder, tempRepack);
        File.Copy(tempRepack, xlsxPath, overwrite: true);
        File.Delete(tempRepack);
        Directory.Delete(tempFolder, recursive: true);
        Console.WriteLine($"Tracking (1).xlsx successfully updated ({new FileInfo(xlsxPath).Length} bytes).");
    }

    #endregion

    #region Update SRS Docx

    static void UpdateSrsDocx(string docxPath)
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "srs_upd_" + Guid.NewGuid().ToString("N"));
        ZipFile.ExtractToDirectory(docxPath, tempFolder);
        string documentXmlPath = Path.Combine(tempFolder, "word", "document.xml");

        var doc = XDocument.Load(documentXmlPath);
        var body = doc.Root?.Element(w + "body");
        if (body == null) throw new InvalidOperationException("Invalid docx: body not found.");

        var tables = body.Elements(w + "tbl").ToList();

        // 1. Table 0: Change Log
        var t0 = tables[0];
        t0.Add(CreateTableRow(new[] {
            "09/11/2026", "M", "TinDLB, ThaiTQ",
            "Updated Iteration 3: Synchronized exact numbering with Tracking Sheet 2 (II.9 Voucher, II.10 Cashier Dashboard, II.11 POS Counter, II.19 Stock Alerts, II.20 Staff, II.21 Order Monitoring, II.22 Admin Dashboard, II.23 Production Tasks Edit/Cancel, II.24 Notifications, II.25 Revenue Report with CSV Export, II.26 Invoices with HTML Download, II.27 Online Orders, II.28 Order Status Update with Customer Notification, II.29 Delivery Assignment with Customer Notification); updated Table 4.2 with all 40 Use Cases UC-01 to UC-40."
        }, new[] { "1500", "800", "1500", "5400" }, false));

        // 2. Table 5: 4.2 Use Cases - Replace all rows after header with EXACT 40 Use Cases from Tracking Sheet 1
        if (tables.Count > 5)
        {
            var t5 = tables[5];
            var headerRow = t5.Elements(w + "tr").FirstOrDefault();
            t5.Elements(w + "tr").Skip(1).Remove();

            var ucs40 = new List<string[]>
            {
                new[] { "UC-01", "Login & Register", "Authentication", "User registers a new account and logs in with role-based access" },
                new[] { "UC-02", "Home Page & Product Detail", "Online Shopping", "Customer browses product list and views details of each item" },
                new[] { "UC-03", "Search & Filter Products", "Online Shopping", "Customer searches by name and filters by category or price" },
                new[] { "UC-04", "Shopping Cart", "Online Shopping", "Customer adds removes and adjusts quantity of items in cart" },
                new[] { "UC-05", "My Account & Address Management", "Customer Profile", "Customer updates personal info and manages multiple delivery addresses" },
                new[] { "UC-06", "Checkout & Online Payment", "Online Shopping", "Customer selects address applies voucher and completes COD or online payment" },
                new[] { "UC-07", "Order History & Tracking", "Online Shopping", "Customer views past orders and tracks current delivery status" },
                new[] { "UC-08", "User Notifications", "Notification", "Customer receives alerts when order is placed delivering and delivered" },
                new[] { "UC-09", "Staff Profile", "Staff Management", "Cashier views and edits own account info and changes password" },
                new[] { "UC-10", "Cashier Dashboard", "Sales & POS", "Cashier sees today total sales summary and count of pending orders" },
                new[] { "UC-11", "POS / Counter Order", "Sales & POS", "Cashier creates a walk-in order selects items and receives payment" },
                new[] { "UC-12", "Invoice Generation", "Invoicing", "System auto-generates, prints and downloads HTML invoice after order is completed" },
                new[] { "UC-13", "Voucher Management", "Promotions", "Admin creates edits and activates or deactivates voucher codes" },
                new[] { "UC-14", "Order Management Online", "Order Fulfillment", "Cashier views and filters the list of pending online orders" },
                new[] { "UC-15", "Order Status Update Pack and Prepare", "Order Fulfillment", "Cashier updates online order status from Pending to Preparing, Delivering, Completed, and notifies customer" },
                new[] { "UC-16", "Delivery & Shipper Management", "Delivery Management", "Cashier assigns Shipper info and marks order as Delivering or Completed, and notifies customer" },
                new[] { "UC-17", "Supplier Management", "Supplier Management", "Admin adds edits and views the list of ingredient suppliers" },
                new[] { "UC-18", "Goods Receipt Import Stock", "Inventory Management", "Admin creates a goods receipt to import ingredients from a supplier" },
                new[] { "UC-19", "Inventory Dashboard & Stock Alert", "Inventory Management", "Admin views current ingredient stock levels with low-stock warnings" },
                new[] { "UC-20", "Cost Report", "Reporting", "Admin views ingredient import cost report filtered by date range" },
                new[] { "UC-21", "Category & Product Management", "Master Data Management", "Admin manages product categories and product details name image price status" },
                new[] { "UC-22", "Recipe Management", "Production Management", "Admin manages production recipes linking products to ingredient quantities" },
                new[] { "UC-23", "Production Planning Task Creation", "Production Management", "Admin creates production tasks and system auto-calculates ingredient needs" },
                new[] { "UC-24", "Admin Stock Alert Notification", "Notification", "System sends alert to Admin when ingredient stock drops below minimum threshold" },
                new[] { "UC-25", "Ingredient Management", "Inventory Management", "Admin manages ingredient list with unit and minimum stock level setting" },
                new[] { "UC-26", "Baker Dashboard", "Production Management", "Baker views the full list of production tasks assigned by Admin" },
                new[] { "UC-27", "Production Execution & Yield Update", "Production Management", "Baker starts task system deducts ingredients and inputs actual yield produced" },
                new[] { "UC-28", "Production History", "Production Management", "Baker and Admin view completed production task history and yield vs plan" },
                new[] { "UC-29", "Loyalty Points", "Customer Loyalty", "Customer earns and redeems loyalty points during checkout" },
                new[] { "UC-30", "Revenue Report", "Reporting", "Admin views consolidated revenue report by date range and downloads CSV export" },
                new[] { "UC-31", "Purchase Order Management", "Procurement", "Admin creates and tracks purchase orders to suppliers; supports partial deliveries under the same PO code and PO cancellation" },
                new[] { "UC-32", "Goods Inspection & Quality Rejection", "Quality Assurance", "Admin inspects each delivered batch for quality and expiry, accepts or rejects defective items, and requests a replacement batch from the supplier" },
                new[] { "UC-33", "Manual Stock Adjustment", "Inventory Management", "Admin manually increases or decreases ingredient stock with a recorded reason such as correction, found stock, damage, expiry, or wastage" },
                new[] { "UC-34", "Stock Count & Reconciliation", "Inventory Management", "Admin performs a physical stock count and reconciles any variance against system quantities" },
                new[] { "UC-35", "Staff Account Management", "Staff Management", "Admin creates Baker and Cashier accounts, resets passwords and activates or deactivates staff accounts" },
                new[] { "UC-36", "Finished Product Stock Monitoring", "Inventory Management", "Admin views finished cake stock per product variant against threshold together with recent sales and sees a low stock flag to decide whether to produce" },
                new[] { "UC-37", "Production Task Management (Edit / Reassign / Cancel)", "Production Management", "Admin views and filters production tasks and edits, reassigns or cancels a Planned task; system updates the task and notifies the Baker" },
                new[] { "UC-38", "Production Notifications", "Production Management", "System notifies Baker when a task is assigned, updated or cancelled and notifies Admin when a task is completed or blocked from starting" },
                new[] { "UC-39", "Admin Dashboard", "Reporting & Monitoring", "Admin sees a summary of today orders, revenue, low-stock items and production status on one overview page" },
                new[] { "UC-40", "Order Monitoring (Admin)", "Order Management", "Admin views and filters all online and counter orders with status and payment information in read-only mode" }
            };

            string[] ucsWidths = new[] { "1000", "2200", "2000", "4000" };
            foreach (var uc in ucs40)
            {
                t5.Add(CreateTableRow(uc, ucsWidths, false));
            }
        }

        // 3. Table 6: Permission Matrix
        if (tables.Count > 6)
        {
            var t6 = tables[6];
            var newPerms = new List<string[]>
            {
                new[] { "Staff accounts (Cashier, Baker)", "Create, edit, activate/deactivate", "Full", "No", "No", "No" },
                new[] { "Vouchers", "Create, edit, toggle active", "Full", "No", "Apply", "Apply" },
                new[] { "Production Task (Edit/Reassign/Cancel)", "Update planned qty, baker, cancel", "Full", "No", "No", "No" },
                new[] { "Order Monitoring (All orders)", "View, search, filter", "Full", "No", "Manage", "Own" },
                new[] { "Online Order Fulfillment", "Prepare, deliver, complete, cancel", "View", "No", "Full", "Cancel Pending" },
                new[] { "Delivery Staff Assignment", "Assign name & phone", "View", "No", "Full", "View" },
                new[] { "Counter (POS) Order", "Create, cash checkout, auto-invoice", "View", "No", "Full", "No" },
                new[] { "Invoice", "Generate, print, download HTML", "View", "No", "Full", "View" },
                new[] { "Notifications (Admin)", "Receive alerts, view, mark read", "Full", "No", "No", "No" },
                new[] { "Notifications (Customer)", "Receive order & delivery alerts", "No", "No", "Trigger", "Full" },
                new[] { "Revenue Report & CSV Export", "View metrics, download CSV", "Full", "No", "No", "No" },
                new[] { "Cashier Dashboard", "View fulfillment & daily sales", "No", "No", "Full", "No" }
            };

            foreach (var p in newPerms)
            {
                bool exists = t6.Elements(w + "tr").Any(r => r.Elements(w + "tc").FirstOrDefault()?.Value.Trim() == p[0]);
                if (!exists)
                {
                    t6.Add(CreateTableRow(p, new[] { "2200", "2000", "1200", "1200", "1200", "1400" }, false));
                }
            }
        }

        // 4. Table 7: Screen List
        if (tables.Count > 7)
        {
            var t7 = tables[7];
            var screens = new List<string[]>
            {
                new[] { "3", "Staff Account Management", "Staff Administration", "Lists Cashier and Baker accounts, search by name/email, toggle active status, and access create/edit forms." },
                new[] { "4", "Create / Edit Staff", "Staff Administration", "Form to input staff FullName, Email, Phone, Role (Cashier/Baker), Password, and active status." },
                new[] { "5", "Voucher Management", "Promotions", "Lists vouchers, code, discount type/value, date validity, usage limit, and active toggle." },
                new[] { "6", "Create / Edit Voucher", "Promotions", "Form to define voucher code, discount percentage or fixed amount, minimum order value, max discount, start/end dates, and usage limit." },
                new[] { "7", "Edit Production Task", "Production Management", "Form allowing Admin to adjust planned quantity, reassign Baker, or cancel task with inventory checking." },
                new[] { "8", "Admin Notifications", "Notifications", "Inbox showing admin notifications (low stock, staff changes, order cancellations, production events) with unread filtering." },
                new[] { "9", "Admin Orders", "Order Monitoring", "Centralized view of all online and offline orders with channel, status, customer, payment details, and date filters." },
                new[] { "10", "Admin Dashboard", "Analytics & Overview", "Executive summary with KPI cards (revenue, orders, active users), low stock alert list, and recent orders." },
                new[] { "11", "Admin Report & Export", "Reporting", "Date-range filtered revenue and volume report with single-click CSV export download." },
                new[] { "12", "Cashier Dashboard", "Sales Overview", "Cashier home screen showing today's revenue and counts of orders in Pending, Preparing, Delivering, and Completed states." },
                new[] { "13", "POS / Counter Order", "Counter Sales", "Direct counter POS interface to select products, specify quantities, apply voucher, record cash payment, and generate invoice." },
                new[] { "14", "Cashier Online Orders", "Fulfillment", "Fulfillment management grid with tabbed status filtering, search by order code or customer phone, and quick status action buttons." },
                new[] { "15", "Cashier Order Details", "Fulfillment", "Detailed order inspection displaying customer, items, payment status, status transition action buttons, and delivery assignment form." },
                new[] { "16", "Assign Delivery Staff", "Delivery Management", "Modal / form to input delivery staff name and phone number for orders in Delivering status." },
                new[] { "17", "Cashier Invoice & Download", "Invoicing", "Sales invoice display with printable layout and direct HTML download button." }
            };

            foreach (var sc in screens)
            {
                bool exists = t7.Elements(w + "tr").Any(r => r.Elements(w + "tc").Skip(1).FirstOrDefault()?.Value.Trim() == sc[1]);
                if (!exists)
                {
                    t7.Add(CreateTableRow(sc, new[] { "800", "2400", "2000", "4000" }, false));
                }
            }
        }

        // 5. Section II: Functional Requirements
        // Find anchor right before Section III
        var anchor = body.Elements(w + "p").FirstOrDefault(p =>
            p.Descendants(w + "t").Any(t => t.Value.Contains("III. Requirement Appendix") || t.Value.Contains("III.  Requirement Appendix") || t.Value.StartsWith("III.")));

        void InsertNode(XElement elem)
        {
            if (anchor != null) anchor.AddBeforeSelf(elem);
            else body.Add(elem);
        }

        // Add Section II Subsections according to Tracking Sheet 2 numbers!

        // II.9: Voucher Management (UC-13 - TinDLB)
        InsertNode(CreateParagraph("II.9 Voucher Management (UC-13)", "Heading2", true));
        InsertNode(CreateParagraph("II.9.1 Screen Design & Field Description", "Heading3", true));
        var voucherFields = new List<string[]>
        {
            new[] { "Field Name", "Description" },
            new[] { "Code", "Unique uppercase alphanumeric voucher code (e.g., WELCOME10). Required." },
            new[] { "DiscountType", "Discount mechanism: Percentage (%) or FixedAmount (VND). Required." },
            new[] { "DiscountValue", "Discount value. Percentage (1-100) or Fixed Amount (> 0). Required." },
            new[] { "MinOrderValue", "Minimum order subtotal required to apply voucher. Required." },
            new[] { "MaxDiscountAmount", "Maximum discount cap for percentage vouchers. Optional for fixed vouchers." },
            new[] { "StartDate", "Activation date and time when voucher becomes eligible. Required." },
            new[] { "EndDate", "Expiration date and time after which voucher cannot be applied. Required." },
            new[] { "UsageLimit", "Total number of times the voucher can be used across all orders. Required." },
            new[] { "IsActive", "Status toggle to activate or suspend voucher. Required." }
        };
        InsertNode(CreateTable(voucherFields, new[] { "2500", "6700" }));
        InsertNode(CreateParagraph("II.9.2 Functionalities - Manage Vouchers", "Heading3", true));
        InsertNode(CreateUCTable("UC-13", "Voucher Management", "Admin",
            "Allows Admin to configure discount codes and enforce checkout promotion rules.",
            "Admin clicks 'Quản lý Voucher' in Admin navigation.",
            "PRE-01: Admin is logged in.",
            "POST-01: Voucher saved with unique code and constraints. POST-02: Available for counter orders and customer checkout.",
            "1. Admin opens Voucher list (/Voucher/Index).\n2. Admin clicks 'Tạo Voucher mới'.\n3. Admin inputs code, discount type/value, date range, min order value, max discount, and usage limit.\n4. System validates code uniqueness and logical constraints (EndDate > StartDate).\n5. System saves voucher to database.\n6. Admin can edit rules or toggle active status anytime.",
            "A1. Admin suspends voucher: Sets IsActive = false; voucher is immediately rejected at checkout.",
            "E1. Duplicate code: System shows 'Mã voucher đã tồn tại.'\nE2. Invalid date range: System shows 'Ngày kết thúc phải sau ngày bắt đầu.'",
            "High"));

        // II.10: Cashier Dashboard (UC-10 - ThaiTQ)
        InsertNode(CreateParagraph("II.10 Cashier Dashboard (UC-10)", "Heading2", true));
        InsertNode(CreateParagraph("II.10.1 Screen Design & Functionalities", "Heading3", true));
        InsertNode(CreateUCTable("UC-10", "Cashier Dashboard", "Cashier",
            "Provides quick operational view for Cashier: today's completed revenue, pending orders, preparing orders, and delivering orders.",
            "Cashier logs in or clicks 'Tổng quan' on Cashier layout.",
            "PRE-01: User is logged in as Cashier.",
            "POST-01: System displays live order counts and sales totals for current date.",
            "1. Cashier opens /Cashier/Dashboard.\n2. System aggregates today's sales (TotalRevenue, CompletedCount, PendingCount, PreparingCount, DeliveringCount).\n3. Cashier inspects pending workload and clicks shortcut buttons to navigate to POS or Online Orders.",
            "A1. No orders today: System displays 0 for counts and 0 VND revenue.",
            "E1. Session expired: System redirects to login.",
            "High"));

        // II.11: POS / Counter Order (UC-11 - ThaiTQ)
        InsertNode(CreateParagraph("II.11 POS / Counter Order (UC-11)", "Heading2", true));
        InsertNode(CreateParagraph("II.11.1 Screen Design & Field Description", "Heading3", true));
        var counterFields = new List<string[]>
        {
            new[] { "Field Name", "Description" },
            new[] { "Customer", "Optional customer selection. Defaults to Walk-in customer (Khách tại quầy)." },
            new[] { "Selected Products", "List of bakery items and quantities selected by Cashier." },
            new[] { "VoucherCode", "Optional promotion voucher code applied at counter." },
            new[] { "PaymentMethod", "Payment method. Restricted strictly to 'Cash' (Tiền mặt)." },
            new[] { "Subtotal / Total", "Automatically calculated based on item prices and applied discount." },
            new[] { "Status / PaymentStatus", "Auto-set upon submission to Status = Completed, PaymentStatus = Paid." }
        };
        InsertNode(CreateTable(counterFields, new[] { "2500", "6700" }));
        InsertNode(CreateParagraph("II.11.2 Functionalities - POS / Counter Order", "Heading3", true));
        InsertNode(CreateUCTable("UC-11", "POS / Counter Order", "Cashier",
            "Allows Cashier to process direct purchases for walk-in customers with cash payment and instant invoice creation.",
            "Cashier clicks 'Bán hàng tại quầy' in navigation.",
            "PRE-01: Cashier is logged in. PRE-02: Selected products have sufficient stock.",
            "POST-01: Offline order created with Status=Completed and PaymentStatus=Paid. POST-02: Product stock deducted. POST-03: Invoice generated automatically.",
            "1. Cashier opens Counter Order screen (/Cashier/CounterOrder).\n2. Cashier adds products and specifies quantities.\n3. If customer provides voucher, Cashier enters voucher code.\n4. System validates product stock and calculates total amount.\n5. Cashier receives cash payment from customer and submits order.\n6. System saves Order with Channel='Offline', PaymentMethod='Cash', deducts stock, and creates Invoice.\n7. System redirects to invoice view for printing.",
            "A1. Cashier applies voucher: System validates voucher rules and deducts discount.",
            "E1. Product quantity exceeds stock: System displays error and blocks checkout.\nE2. Non-cash payment selected: System rejects transaction.",
            "High"));

        // II.19: Admin Stock Alert Notification (UC-24 - TinDLB)
        InsertNode(CreateParagraph("II.19 Admin Stock Alert Notification (UC-24)", "Heading2", true));
        InsertNode(CreateParagraph("II.19.1 Functionalities - Stock Alert Notifications", "Heading3", true));
        InsertNode(CreateUCTable("UC-24", "Admin Stock Alert Notification", "System / Admin",
            "System automatically sends an alert to Admin when ingredient stock drops below its minimum threshold.",
            "Triggered whenever an ingredient's StockQty falls below MinThreshold following production or stock adjustment.",
            "PRE-01: Ingredient exists with MinThreshold defined.",
            "POST-01: Notification created with type 'LowStock' and delivered to all active Admin accounts.",
            "1. System detects ingredient stock level <= MinThreshold during stock deduction.\n2. System calls NotificationService.NotifyAdminsAsync('LowStock', title, message).\n3. System inserts Notification record with IsRead = false for each active Admin.\n4. Admin opens /Admin/Notifications, views alert, and navigates to Inventory to reorder.",
            "A1. Stock replenished: Notification marked as read by Admin.",
            "E1. Admin user inactive: Notification is not created for inactive users.",
            "High"));

        // II.20: Staff Account Management (UC-35 - TinDLB)
        InsertNode(CreateParagraph("II.20 Staff Account Management (UC-35)", "Heading2", true));
        InsertNode(CreateParagraph("II.20.1 Screen Design & Field Description", "Heading3", true));
        var staffFields = new List<string[]>
        {
            new[] { "Field Name", "Description" },
            new[] { "FullName", "Full name of staff member (2 to 100 characters). Required." },
            new[] { "Email", "Login email address. Must be unique across all users. Required." },
            new[] { "Phone", "Vietnamese 10-digit phone number (starts with 03, 05, 07, 08, 09). Required." },
            new[] { "Role", "System role for staff: Cashier or Baker. Admin role cannot be created here. Required." },
            new[] { "Password", "Account password (minimum 6 characters). Stored as BCrypt hash. Required on create, optional on edit." },
            new[] { "IsActive", "Staff active state. Inactive staff cannot log in or be assigned production tasks. Default: Active." }
        };
        InsertNode(CreateTable(staffFields, new[] { "2500", "6700" }));
        InsertNode(CreateParagraph("II.20.2 Functionalities - Staff Account Management", "Heading3", true));
        InsertNode(CreateUCTable("UC-35", "Staff Account Management", "Admin",
            "Allows Admin to list, create, edit, and toggle active status of Cashier and Baker accounts.",
            "Admin clicks 'Quản lý nhân viên' on the Admin sidebar.",
            "PRE-01: Admin is authenticated with Admin role.",
            "POST-01: New staff account created with BCrypt password hash. POST-02: Existing staff details or active status updated. POST-03: System sends notification to Admin.",
            "1. Admin navigates to Staff Management screen (/Staff/Index).\n2. System retrieves and displays list of Cashier and Baker users.\n3. To add new staff, Admin clicks 'Thêm nhân viên mới', fills form, and submits.\n4. System validates email uniqueness and format rules.\n5. System hashes password using BCrypt and persists user to database.\n6. To toggle active status, Admin clicks 'Khóa' / 'Mở khóa' on staff row.\n7. System toggles IsActive flag and generates notification.",
            "A1. Admin edits existing staff: Admin clicks 'Chỉnh sửa', modifies name/phone/role/password, and saves.",
            "E1. Email already exists: System displays 'Email này đã được sử dụng.'\nE2. Invalid phone format: System displays validation error.",
            "High"));

        // II.21: Order Monitoring (Admin) (UC-40 - TinDLB)
        InsertNode(CreateParagraph("II.21 Order Monitoring (Admin) (UC-40)", "Heading2", true));
        InsertNode(CreateParagraph("II.21.1 Functionalities - Admin Order Monitoring", "Heading3", true));
        InsertNode(CreateUCTable("UC-40", "Order Monitoring (Admin)", "Admin",
            "Allows Admin to inspect and monitor all customer and counter orders across channels in read-only mode.",
            "Admin clicks 'Đơn hàng' on Admin navigation bar.",
            "PRE-01: Admin is authenticated.",
            "POST-01: System renders filtered orders with real-time status and financial metrics.",
            "1. Admin accesses /Admin/Orders.\n2. System loads orders with customer, channel, total amount, status, and payment status.\n3. Admin filters by Channel (Online/Offline), Status (Pending, Preparing, Delivering, Completed, Cancelled), or date.\n4. Admin clicks on order code to view full order details including delivery staff and invoice.",
            "A1. Admin views counter orders only to audit daily POS cashier receipts.",
            "E1. No orders matching filter: System displays empty result message.",
            "High"));

        // II.22: Admin Dashboard (UC-39 - TinDLB)
        InsertNode(CreateParagraph("II.22 Admin Dashboard (UC-39)", "Heading2", true));
        InsertNode(CreateParagraph("II.22.1 Functionalities - Admin Dashboard", "Heading3", true));
        InsertNode(CreateUCTable("UC-39", "Admin Dashboard", "Admin",
            "Provides executive dashboard summarizing today's orders, revenue, active accounts, low-stock warnings, and recent activity.",
            "Admin logs in or opens /Admin/Dashboard.",
            "PRE-01: Admin is logged in.",
            "POST-01: Operational overview displayed with real-time metrics.",
            "1. Admin accesses /Admin/Dashboard.\n2. System computes Total Products, Today's Orders, Active Users, and Completed Paid Revenue.\n3. System queries low-stock ingredients and displays warning table.\n4. System lists recent 5 orders with quick link to details.\n5. Admin reviews metrics and triggers operational actions as necessary.",
            "A1. No low-stock items: System shows green status banner.",
            "E1. Database unreachable: System returns error page.",
            "High"));

        // II.23: Production Task Management (Edit / Reassign / Cancel) (UC-37 - TinDLB)
        InsertNode(CreateParagraph("II.23 Production Task Management (Edit / Reassign / Cancel) (UC-37)", "Heading2", true));
        InsertNode(CreateParagraph("II.23.1 Functionalities - Production Task Management", "Heading3", true));
        InsertNode(CreateUCTable("UC-37", "Production Task Management (Edit / Reassign / Cancel)", "Admin",
            "Allows Admin to adjust planned quantities, reassign bakers, or cancel pending production tasks.",
            "Admin views Production Tasks and clicks 'Chỉnh sửa' or 'Hủy bỏ' on an active task.",
            "PRE-01: Task is in Planned or InProgress status. Completed tasks cannot be modified.",
            "POST-01: Planned quantity or assigned Baker updated in database. POST-02: Notifications dispatched to Baker and Admin.",
            "1. Admin navigates to /ProductionTask/Index.\n2. Admin clicks 'Chỉnh sửa' on a task.\n3. Admin modifies TargetQuantity, DueDate, or assigns a different active Baker.\n4. System verifies that required ingredient inventory is sufficient for updated target quantity.\n5. Admin confirms and clicks 'Lưu thay đổi'.\n6. System updates ProductionTask and recalculates required ingredient quantities.\n7. System sends 'TaskUpdated' notification to assigned Baker.",
            "A1. Admin cancels task: Admin clicks 'Hủy nhiệm vụ'. System sets status to 'Cancelled' and sends cancellation notification to Baker.",
            "E1. Insufficient ingredients: System displays warning and caps target quantity to maximum producible quantity.\nE2. Task already completed: System rejects modification.",
            "High"));

        // II.24: Production Notifications (UC-38 - TinDLB)
        InsertNode(CreateParagraph("II.24 Production Notifications (UC-38)", "Heading2", true));
        InsertNode(CreateParagraph("II.24.1 Functionalities - Production Notifications", "Heading3", true));
        InsertNode(CreateUCTable("UC-38", "Production Notifications", "Baker / Admin",
            "System notifies Baker when a task is assigned, updated or cancelled, and notifies Admin when a task is completed or blocked.",
            "Triggered by production task lifecycle events.",
            "PRE-01: User accounts exist and are active.",
            "POST-01: Targeted notification inserted in Notifications table and visible in recipient inbox.",
            "1. When Admin assigns/updates a task, system calls NotificationService.CreateNotificationAsync for assigned Baker.\n2. When Baker starts or completes a task, system calls NotificationService.NotifyAdminsAsync.\n3. Recipient opens notification inbox and marks items as read.",
            "A1. Task cancelled by Admin: Baker receives immediate cancellation notification.",
            "E1. Notification failed: Error is logged without interrupting the transaction.",
            "Medium"));

        // II.25: Revenue Report (UC-30 - TinDLB) (kèm Tải báo cáo CSV của Admin)
        InsertNode(CreateParagraph("II.25 Revenue Report & CSV Export Download (UC-30)", "Heading2", true));
        InsertNode(CreateParagraph("II.25.1 Screen Design & Field Description", "Heading3", true));
        var reportFields = new List<string[]>
        {
            new[] { "Field / Parameter", "Description" },
            new[] { "From Date", "Start date for report calculation. Defaults to 29 days prior to today." },
            new[] { "To Date", "End date for report calculation. Defaults to today." },
            new[] { "OrderCount", "Total number of orders created within selected range." },
            new[] { "CompletedOrderCount", "Number of orders successfully delivered/completed." },
            new[] { "CancelledOrderCount", "Number of cancelled orders." },
            new[] { "NewUserCount", "Number of new customer accounts registered within selected range." },
            new[] { "Revenue", "Total paid revenue from completed orders within selected range." },
            new[] { "Export CSV Button", "Generates and downloads bms-report-YYYYMMDD-YYYYMMDD.csv containing OrderCode, Channel, Status, PaymentStatus, TotalAmount, and CreatedAt." }
        };
        InsertNode(CreateTable(reportFields, new[] { "2500", "6700" }));
        InsertNode(CreateParagraph("II.25.2 Functionalities - Revenue Report & CSV Export Download", "Heading3", true));
        InsertNode(CreateUCTable("UC-30", "Revenue Report", "Admin",
            "Displays revenue metrics and exports complete order dataset as a UTF-8 CSV file (Tải báo cáo của admin).",
            "Admin clicks 'Báo cáo doanh thu' on Admin navigation bar.",
            "PRE-01: Admin is logged in.",
            "POST-01: Financial metrics computed and displayed. POST-02: CSV file generated and downloaded to Admin browser.",
            "1. Admin navigates to /Admin/Report.\n2. Admin selects From Date and To Date and clicks 'Lọc dữ liệu'.\n3. System queries Orders and Users within range and computes KPI cards.\n4. Admin clicks 'Xuất báo cáo (CSV)'.\n5. System calls /Admin/ExportReport, queries orders, formats records as comma-separated values with escaped quotes.\n6. System returns HTTP 200 with MIME text/csv and Content-Disposition attachment.\n7. Browser downloads file named bms-report-[StartDate]-[EndDate].csv.",
            "A1. Admin runs report with empty dates: System defaults to last 30 days.",
            "E1. Date range invalid (From > To): System adjusts or returns empty summary without error.",
            "High"));

        // II.26: Invoice Generation (UC-12 - ThaiTQ)
        InsertNode(CreateParagraph("II.26 Invoice Generation (UC-12)", "Heading2", true));
        InsertNode(CreateParagraph("II.26.1 Functionalities - Invoice Generation, Viewing & HTML Download", "Heading3", true));
        InsertNode(CreateUCTable("UC-12", "Invoice Generation", "Cashier",
            "Auto-generates, views, prints, and downloads sales invoice after order is completed and paid.",
            "Cashier clicks 'Xuất hóa đơn' or 'Tải hóa đơn' on completed order.",
            "PRE-01: Order status is 'Completed' and payment status is 'Paid'.",
            "POST-01: Unique Invoice record created with sequential InvoiceNo. POST-02: Formatted HTML invoice rendered or downloaded.",
            "1. Cashier inspects completed order and clicks 'Xuất hóa đơn'.\n2. System checks if Invoice already exists; if not, creates Invoice entity with unique InvoiceNo (INV-YYYYMMDD-XXXX).\n3. Cashier views invoice containing store info, invoice number, order code, recipient name/phone, delivery address, payment method/status, delivery staff name/phone, line items, and totals.\n4. Cashier clicks 'Tải hóa đơn (HTML)'.\n5. System calls /Cashier/DownloadInvoice, generates UTF-8 HTML document, and returns downloadable attachment [InvoiceNo].html.",
            "A1. Cashier prints invoice: Clicks 'In hóa đơn' to trigger browser print dialog.",
            "E1. Order not completed or unpaid: System refuses invoice creation.",
            "High"));

        // II.27: Order Management Online List & Filter (UC-14 - ThaiTQ)
        InsertNode(CreateParagraph("II.27 Order Management Online List & Filter (UC-14)", "Heading2", true));
        InsertNode(CreateParagraph("II.27.1 Functionalities - Cashier Order Management Online", "Heading3", true));
        InsertNode(CreateUCTable("UC-14", "Order Management Online", "Cashier",
            "Allows Cashier to browse and filter online orders by order status (Pending, Preparing, Delivering, Completed, Cancelled) and search by code or phone.",
            "Cashier clicks 'Đơn hàng online' on Cashier layout.",
            "PRE-01: Cashier is logged in.",
            "POST-01: System renders filtered list with customer name, phone, order code, total amount, and quick action buttons.",
            "1. Cashier navigates to /Cashier/Orders.\n2. System displays online orders sorted with latest first.\n3. Cashier filters by tabbed status or searches by keyword.\n4. Cashier selects an order to open details or update status.",
            "A1. Cashier views details: Clicks 'Chi tiết' to open /Cashier/OrderDetails/[id].",
            "E1. No matching records: System shows 'Không có đơn hàng nào'.",
            "High"));

        // II.28: Order Status Update: Pack & Prepare (UC-15 - ThaiTQ) (kèm Thông báo cho khách hàng khi cập nhật đơn)
        InsertNode(CreateParagraph("II.28 Order Status Update: Pack & Prepare (UC-15)", "Heading2", true));
        InsertNode(CreateParagraph("II.28.1 Functionalities - Order Status Update & Customer Notification", "Heading3", true));
        InsertNode(CreateUCTable("UC-15", "Order Status Update Pack and Prepare", "Cashier",
            "Allows Cashier to progress orders through fulfillment lifecycle (Pending -> Preparing -> Delivering -> Completed or Cancelled) and automatically notifies the customer at each milestone (Thông báo cho người dùng khi cập nhật đơn từ cashier).",
            "Cashier clicks status transition button on Cashier Orders screen or Order Details page.",
            "PRE-01: Cashier is logged in. PRE-02: Order exists and requested status transition is valid.",
            "POST-01: Order status updated in database. POST-02: Notification created and delivered to Customer inbox. POST-03: Notification created for Admin.",
            "1. Cashier views order in /Cashier/Orders or /Cashier/OrderDetails.\n2. Cashier verifies items and clicks 'Chuẩn bị đơn', 'Giao hàng', or 'Hoàn thành'.\n3. System opens transaction and validates transition (Pending->Preparing->Delivering->Completed).\n4. System updates Order.Status, CashierId, and completion timestamps.\n5. System calls NotificationService.NotifyAdminsAsync with status event.\n6. If order belongs to a registered customer (order.CustomerId != null), system invokes NotificationService.CreateNotificationAsync with title 'Đơn hàng [OrderCode] đã được cập nhật' and localized status text.\n7. Customer receives notification and can view progress in personal order history.\n8. Cashier view reloads with success message.",
            "A1. Cashier cancels order: Allowed from Pending/Preparing/Delivering; triggers cancellation notification to Customer and Admin.",
            "E1. Invalid status jump (e.g. Pending directly to Completed): System displays 'Trạng thái chuyển đổi không hợp lệ.'\nE2. Concurrent status update by another user: Transaction prevents inconsistent state.",
            "High"));

        // II.29: Delivery & Fulfillment Management (Self-Delivery Staff) (UC-16 - ThaiTQ) (kèm Thông báo người giao hàng cho khách hàng)
        InsertNode(CreateParagraph("II.29 Delivery & Fulfillment Management (Self-Delivery Staff) (UC-16)", "Heading2", true));
        InsertNode(CreateParagraph("II.29.1 Screen Design & Field Description", "Heading3", true));
        var delivFields = new List<string[]>
        {
            new[] { "Field Name", "Description" },
            new[] { "OrderId", "ID of the online order being dispatched. Must be in 'Delivering' status." },
            new[] { "DeliveryStaffName", "Full name of delivery personnel (2 to 100 characters). Required." },
            new[] { "DeliveryStaffPhone", "Valid 10-digit phone number of delivery personnel. Required." }
        };
        InsertNode(CreateTable(delivFields, new[] { "2500", "6700" }));
        InsertNode(CreateParagraph("II.29.2 Functionalities - Delivery Staff Assignment & Customer Notification", "Heading3", true));
        InsertNode(CreateUCTable("UC-16", "Delivery & Shipper Management", "Cashier",
            "Assigns self-delivery staff contact details to an order and sends notification to the customer (Thông báo cho người dùng khi phân công shipper).",
            "Cashier clicks 'Phân công giao hàng' on an order in Delivering status.",
            "PRE-01: Order is in 'Delivering' status. PRE-02: Cashier is logged in.",
            "POST-01: DeliveryStaffName and DeliveryStaffPhone saved on Order. POST-02: Customer receives notification with shipper details. POST-03: Details appear on invoice.",
            "1. Cashier navigates to /Cashier/AssignDelivery/[id].\n2. System displays order code, recipient name, address, and phone.\n3. Cashier enters Delivery Staff Name and Delivery Staff Phone.\n4. Cashier clicks 'Xác nhận phân công'.\n5. System validates phone number format and saves fields to Order table.\n6. System notifies Admin via NotifyAdminsAsync.\n7. System creates notification for Customer: 'Đơn hàng [OrderCode] đã có người giao hàng - Người giao hàng: [Name], SĐT: [Phone]'.\n8. Customer refreshes order tracking and sees shipper information immediately.",
            "A1. Cashier updates shipper info: Submits form with revised name or phone.",
            "E1. Order not in Delivering status: System blocks assignment.\nE2. Invalid phone number: System shows validation error.",
            "High"));

        doc.Save(documentXmlPath);

        string tempRepack = Path.Combine(Path.GetTempPath(), "srs_repack_" + Guid.NewGuid().ToString("N") + ".docx");
        ZipFile.CreateFromDirectory(tempFolder, tempRepack);
        File.Copy(tempRepack, docxPath, overwrite: true);
        File.Delete(tempRepack);
        Directory.Delete(tempFolder, recursive: true);
        Console.WriteLine($"Report 1 - SRS.docx successfully saved ({new FileInfo(docxPath).Length} bytes).");
    }

    #endregion

    #region Update SDS Docx

    static void UpdateSdsDocx(string docxPath)
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "sds_upd_" + Guid.NewGuid().ToString("N"));
        ZipFile.ExtractToDirectory(docxPath, tempFolder);
        string documentXmlPath = Path.Combine(tempFolder, "word", "document.xml");

        var doc = XDocument.Load(documentXmlPath);
        var body = doc.Root?.Element(w + "body");
        if (body == null) throw new InvalidOperationException("Invalid docx: body not found.");

        var tables = body.Elements(w + "tbl").ToList();

        // 1. Table 0: Change Log
        var t0 = tables[0];
        t0.Add(CreateTableRow(new[] {
            "09/11/2026", "M", "TinDLB, ThaiTQ",
            "Updated Iteration 3 design: Added full Detailed Code Design in Section II matching Tracking Sheet 2: Feature Set 1 (Cashier Sales, Fulfillment, Invoicing & Customer Notification by ThaiTQ - II.2, II.10, II.11, II.26, II.27, II.28, II.29) and Feature Set 2 (Admin Operations, Production Planning & Reassignment, Staff Management, Voucher Management, and Revenue Report with CSV Export by TinDLB - II.3, II.7, II.8, II.9, II.19, II.20, II.21, II.22, II.23, II.24, II.25) with Class Diagrams and Sequence Diagrams."
        }, new[] { "1500", "800", "1500", "5400" }, false));

        // 2. Table 1: Architecture components
        if (tables.Count > 1)
        {
            var t1 = tables[1];
            bool hasStaffCtrl = t1.Elements(w + "tr").Any(r => r.Descendants(w + "t").Any(t => t.Value.Contains("StaffController") || t.Value.Contains("AdminController")));
            if (!hasStaffCtrl)
            {
                t1.Add(CreateTableRow(new[] { "10", "Admin & Staff Controllers", "AdminController (Dashboard, Order Monitoring, Notifications, Report, ExportReport CSV); StaffController (CRUD staff, toggle active); ProductionTaskController (Create, Edit, Cancel); VoucherController (CRUD vouchers, rules)." }, new[] { "800", "2400", "6000" }, false));
                t1.Add(CreateTableRow(new[] { "11", "Cashier & Notification Services", "CashierController (CounterOrder, Orders, OrderDetails, UpdateStatus, AssignDelivery, Invoice, DownloadInvoice); NotificationService (Broadcast admin alerts, send customer status & delivery alerts)." }, new[] { "800", "2400", "6000" }, false));
            }
        }

        // 3. Section II Detailed Code Design
        var sec2H1 = body.Elements(w + "p").FirstOrDefault(p =>
            p.Descendants(w + "t").Any(t => t.Value.Contains("II. Detailed Code Design")));

        var sec3H1 = body.Elements(w + "p").FirstOrDefault(p =>
            p.Descendants(w + "t").Any(t => t.Value.Contains("III. Other Design Specifications") || t.Value.StartsWith("III.")));

        if (sec2H1 != null)
        {
            var nodesToRemove = new List<XNode>();
            var curr = sec2H1.NextNode;
            while (curr != null && curr != sec3H1)
            {
                nodesToRemove.Add(curr);
                curr = curr.NextNode;
            }
            foreach (var n in nodesToRemove)
            {
                n.Remove();
            }

            XNode currentAnchor = sec2H1;
            void InsertAfterCurrent(XElement elem)
            {
                currentAnchor.AddAfterSelf(elem);
                currentAnchor = elem;
            }

            InsertAfterCurrent(CreateParagraph("Detailed design specifications for core operational features involving backend logic, database transactions, role authorization, and notification integrations, mapped directly to Tracking Sheet 2.", null, false));

            // FEATURE 1: Cashier Sales, Fulfillment, Invoicing & Customer Notification (ThaiTQ)
            InsertAfterCurrent(CreateParagraph("1. Cashier Sales, Order Fulfillment, Delivery & Invoicing (ThaiTQ - II.2, II.10, II.11, II.26, II.27, II.28, II.29)", "Heading2", true));
            InsertAfterCurrent(CreateParagraph("1.1 Class Diagram - Cashier Module", "Heading3", true));
            InsertAfterCurrent(CreateParagraph("The class design covers cashier sales processing, online order lifecycle management, delivery assignment, and invoice document generation."));

            var cashierClassDesc = new List<string[]>
            {
                new[] { "Class / Interface", "Type", "Responsibilities & Key Members" },
                new[] { "CashierController", "Controller", "Handles counter sales, order listing, status transitions, delivery staff assignment, invoice rendering and HTML download.\n- CounterOrder(GET/POST)\n- Orders(channel, status, search, page)\n- OrderDetails(id)\n- UpdateOrderStatus(id, status) [Serializable Tx]\n- AssignDelivery(model) [Validates Delivering status]\n- GenerateInvoice(orderId)\n- Invoice(id), DownloadInvoice(id)" },
                new[] { "INotificationService", "Interface", "Defines notification contracts:\n- NotifyAdminsAsync(type, title, message)\n- CreateNotificationAsync(userId, type, title, message)" },
                new[] { "NotificationService", "Service", "Implementation creating persistent records in Notifications table for target recipients." },
                new[] { "BakeryManagementDbContext", "DbContext", "EF Core unit of work managing Orders, OrderItems, Invoices, Products, Vouchers, Addresses, and Notifications." },
                new[] { "CounterOrderViewModel", "ViewModel", "Data transfer object for POS sales: SelectedProducts, Quantities, VoucherCode, PaymentMethod (restricted to Cash), Subtotal, TotalAmount." },
                new[] { "CashierOrderDetailsViewModel", "ViewModel", "Encapsulates order details: Customer, ShippingAddress, LineItems, Status, DeliveryStaffName, DeliveryStaffPhone, Invoice status." },
                new[] { "AssignDeliveryViewModel", "ViewModel", "Carries OrderId, DeliveryStaffName (required), DeliveryStaffPhone (required 10-digit format)." },
                new[] { "Order", "Entity", "Database model: OrderId, OrderCode, Channel, CustomerId, CashierId, AddressId, Status, PaymentMethod, PaymentStatus, DeliveryStaffName, DeliveryStaffPhone, TotalAmount." },
                new[] { "Invoice", "Entity", "Sales invoice model: InvoiceId, OrderId, InvoiceNo, IssuedAt, TotalAmount." }
            };
            InsertAfterCurrent(CreateTable(cashierClassDesc, new[] { "2200", "1500", "5500" }));

            InsertAfterCurrent(CreateParagraph("Class Relationships:\n- CashierController IS-A Microsoft.AspNetCore.Mvc.Controller\n- CashierController HAS-A BakeryManagementDbContext (dependency injection)\n- CashierController HAS-A INotificationService (dependency injection)\n- Order HAS-A Invoice (1-to-0..1 relationship)\n- Order HAS-MANY OrderItems (1-to-many relationship)\n- Order ASSOCIATED-WITH User (Customer, Cashier) and Address", null, false));

            // Sequence Diagram 1.2: POS Counter Order (II.11 - UC-11)
            InsertAfterCurrent(CreateParagraph("1.2 Sequence Diagram: POS Counter Order Checkout (II.11 - UC-11)", "Heading3", true));
            InsertAfterCurrent(CreateParagraph("Workflow for walk-in counter sales:\n1. Cashier -> CashierController: HTTP POST /Cashier/CounterOrder(model)\n2. CashierController -> BakeryManagementDbContext: Query selected Products and check available StockQty\n3. CashierController: Validate stock >= requested quantity and PaymentMethod == 'Cash'\n4. CashierController -> VoucherService: If voucher entered, validate eligibility and calculate discount\n5. CashierController -> DbContext: Begin Database Transaction\n6. CashierController -> Product: Decrement StockQty for each item\n7. CashierController -> DbContext: Insert Order (Channel='Offline', Status='Completed', PaymentStatus='Paid', PaymentMethod='Cash')\n8. CashierController -> DbContext: Insert OrderItems and Invoice (InvoiceNo='INV-YYYYMMDD-XXXX')\n9. CashierController -> DbContext: SaveChangesAsync() and Commit Transaction\n10. CashierController -> Browser: RedirectToAction(Invoice, new { id = order.OrderId })"));

            // Sequence Diagram 1.3: Order Status Update & Customer Notification (II.28 - UC-15)
            InsertAfterCurrent(CreateParagraph("1.3 Sequence Diagram: Online Order Lifecycle Update with Customer Notification (II.28 - UC-15)", "Heading3", true));
            InsertAfterCurrent(CreateParagraph("Fulfillment status transition and customer alert flow (Thông báo cho người dùng khi cập nhật đơn từ cashier):\n1. Cashier -> CashierController: HTTP POST /Cashier/UpdateOrderStatus(id, newStatus)\n2. CashierController -> DbContext: Begin Transaction (IsolationLevel.Serializable)\n3. CashierController -> DbContext: Query Order by id\n4. CashierController: Validate IsValidStatusTransition(current, next) -> [Pending -> Preparing -> Delivering -> Completed]\n5. CashierController: Update Order.Status, Order.CashierId, and completion timestamp if Completed\n6. CashierController -> DbContext: SaveChangesAsync() and Commit Transaction\n7. CashierController -> INotificationService: NotifyAdminsAsync('OrderStatusChanged', title, message)\n8. CashierController -> INotificationService: If Order.CustomerId != null, invoke CreateNotificationAsync(order.CustomerId.Value, 'OrderStatusChanged', 'Đơn hàng ' + order.OrderCode + ' đã được cập nhật', 'Trạng thái hiện tại: ' + statusText)\n9. NotificationService -> DbContext: Insert Notification record for customer and save\n10. CashierController -> Browser: RedirectToAction(OrderDetails, new { id }) with Success TempData\n11. Customer: On page refresh / inbox visit, reads updated order status and received notification"));

            // Sequence Diagram 1.4: Delivery Staff Assignment & Customer Notification (II.29 - UC-16)
            InsertAfterCurrent(CreateParagraph("1.4 Sequence Diagram: Delivery Staff Assignment & Customer Notification (II.29 - UC-16)", "Heading3", true));
            InsertAfterCurrent(CreateParagraph("Assigning self-delivery staff to dispatched orders (Thông báo cho người dùng khi phân công shipper):\n1. Cashier -> CashierController: HTTP POST /Cashier/AssignDelivery(model)\n2. CashierController -> DbContext: Query Order by model.OrderId\n3. CashierController: Validate Order.Status == 'Delivering' and model.DeliveryStaffPhone matches 10-digit format\n4. CashierController: Set Order.DeliveryStaffName = model.DeliveryStaffName, Order.DeliveryStaffPhone = model.DeliveryStaffPhone\n5. CashierController -> DbContext: SaveChangesAsync()\n6. CashierController -> INotificationService: NotifyAdminsAsync('DeliveryAssigned', 'Đã cập nhật người giao hàng', message)\n7. CashierController -> INotificationService: If Order.CustomerId != null, invoke CreateNotificationAsync(order.CustomerId.Value, 'DeliveryAssigned', 'Đơn hàng ' + order.OrderCode + ' đã có người giao hàng', 'Người giao hàng: ' + model.DeliveryStaffName + '. SĐT: ' + model.DeliveryStaffPhone)\n8. NotificationService -> DbContext: Insert Notification row for Customer\n9. CashierController -> Browser: RedirectToAction(OrderDetails, new { id = model.OrderId })"));

            // Sequence Diagram 1.5: Invoice Generation & Download HTML (II.26 - UC-12)
            InsertAfterCurrent(CreateParagraph("1.5 Sequence Diagram: Invoice Generation & HTML Export Download (II.26 - UC-12)", "Heading3", true));
            InsertAfterCurrent(CreateParagraph("Generating and exporting sales invoice:\n1. Cashier -> CashierController: HTTP GET /Cashier/DownloadInvoice(id)\n2. CashierController -> DbContext: Query Order including Customer, Address, Invoice, OrderItems, Products\n3. CashierController: Verify Invoice exists and Order is Paid\n4. CashierController: Construct complete HTML invoice with header, order code, recipient name, delivery phone, delivery address, delivery staff, payment status, item table, and totals\n5. CashierController: Encode all strings with WebUtility.HtmlEncode to prevent injection\n6. CashierController -> Browser: Return File(Encoding.UTF8.GetBytes(html), 'text/html', order.Invoice.InvoiceNo + '.html')\n7. Browser: Initiates file download"));

            // FEATURE 2: Admin Operations, Production, Staff & Reporting (TinDLB)
            InsertAfterCurrent(CreateParagraph("2. Admin Operations, Production Planning, Staff & Reporting (TinDLB - II.3, II.7, II.8, II.9, II.19, II.20, II.21, II.22, II.23, II.24, II.25)", "Heading2", true));
            InsertAfterCurrent(CreateParagraph("2.1 Class Diagram - Admin & Production Module", "Heading3", true));

            var adminClassDesc = new List<string[]>
            {
                new[] { "Class / Interface", "Type", "Responsibilities & Key Members" },
                new[] { "AdminController", "Controller", "Handles Admin Dashboard, centralized Order Monitoring, Notification inbox, and Revenue Report with CSV export.\n- Dashboard()\n- Orders(channel, status, from, to, page)\n- Notifications(unreadOnly)\n- Report(from, to)\n- ExportReport(from, to)" },
                new[] { "ProductionTaskController", "Controller", "Manages production planning, task creation, quantity modification, baker reassignment, and cancellation.\n- Index()\n- Create(GET/POST)\n- Edit(GET/POST)\n- Cancel(id)" },
                new[] { "StaffController", "Controller", "Manages Cashier and Baker accounts.\n- Index()\n- Create(GET/POST)\n- Edit(GET/POST)\n- ToggleStatus(id)" },
                new[] { "VoucherController", "Controller", "Configures promotions and discount rules.\n- Index(), Create(GET/POST), Edit(GET/POST), ToggleStatus(id)" },
                new[] { "AdminReportViewModel", "ViewModel", "Aggregates KPI metrics: From, To, OrderCount, CompletedOrderCount, CancelledOrderCount, NewUserCount, Revenue." },
                new[] { "ProductionTaskViewModel", "ViewModel", "Carries TargetQuantity, AssignedBakerId, DueDate, Recipe details, and inventory availability checks." },
                new[] { "StaffFormViewModel", "ViewModel", "Carries FullName, Email, Phone, Role (Cashier/Baker), Password, and IsActive." }
            };
            InsertAfterCurrent(CreateTable(adminClassDesc, new[] { "2200", "1500", "5500" }));

            InsertAfterCurrent(CreateParagraph("Class Relationships:\n- AdminController, ProductionTaskController, StaffController, VoucherController IS-A Controller\n- Controllers HAS-A BakeryManagementDbContext\n- Controllers HAS-A INotificationService\n- ProductionTask ASSOCIATED-WITH Product, Recipe, and User (Baker)\n- StaffController CREATES/UPDATES User entity with Role in ('Cashier', 'Baker')", null, false));

            // Sequence Diagram 2.2: Staff Account Management (II.20 - UC-35)
            InsertAfterCurrent(CreateParagraph("2.2 Sequence Diagram: Staff Account Creation & Status Management (II.20 - UC-35)", "Heading3", true));
            InsertAfterCurrent(CreateParagraph("Admin staff administration workflow:\n1. Admin -> StaffController: HTTP POST /Staff/Create(model)\n2. StaffController -> DbContext: Check email uniqueness (Users.Any(u => u.Email == model.Email))\n3. StaffController: Hash password using BCrypt.Net.BCrypt.HashPassword(model.Password)\n4. StaffController -> DbContext: Insert User (FullName, Email, Phone, Role, PasswordHash, IsActive=true, CreatedAt=UtcNow)\n5. StaffController -> DbContext: SaveChangesAsync()\n6. StaffController -> INotificationService: NotifyAdminsAsync('StaffCreated', 'Đã thêm nhân viên', message)\n7. StaffController -> Browser: RedirectToAction(Index) with Success Message\n8. Admin -> StaffController: HTTP POST /Staff/ToggleStatus(id)\n9. StaffController -> DbContext: Toggle user.IsActive, save changes, and broadcast notification"));

            // Sequence Diagram 2.3: Production Task Management (II.23 - UC-37, II.24 - UC-38, II.19 - UC-24)
            InsertAfterCurrent(CreateParagraph("2.3 Sequence Diagram: Production Task Management & Notifications (II.23 - UC-37, II.24 - UC-38, II.19 - UC-24)", "Heading3", true));
            InsertAfterCurrent(CreateParagraph("Production task modification and baker notification workflow:\n1. Admin -> ProductionTaskController: HTTP POST /ProductionTask/Edit(model)\n2. ProductionTaskController -> DbContext: Query ProductionTask by model.TaskId\n3. ProductionTaskController: Verify Task.Status != 'Completed'\n4. ProductionTaskController -> DbContext: Check Ingredient stock requirements for revised TargetQuantity\n5. ProductionTaskController: Update Task.TargetQuantity, Task.AssignedBakerId, Task.DueDate\n6. ProductionTaskController -> DbContext: SaveChangesAsync()\n7. ProductionTaskController -> INotificationService: CreateNotificationAsync(model.AssignedBakerId, 'TaskUpdated', 'Cập nhật nhiệm vụ sản xuất', message)\n8. ProductionTaskController -> INotificationService: NotifyAdminsAsync('TaskUpdated', 'Đã cập nhật nhiệm vụ', message)\n9. ProductionTaskController -> Browser: RedirectToAction(Index)"));

            // Sequence Diagram 2.4: Voucher Management (II.9 - UC-13)
            InsertAfterCurrent(CreateParagraph("2.4 Sequence Diagram: Voucher Creation & Validation (II.9 - UC-13)", "Heading3", true));
            InsertAfterCurrent(CreateParagraph("Promotion configuration workflow:\n1. Admin -> VoucherController: HTTP POST /Voucher/Create(model)\n2. VoucherController -> DbContext: Verify Code uniqueness\n3. VoucherController: Validate EndDate > StartDate, DiscountValue > 0, MinOrderValue >= 0\n4. VoucherController -> DbContext: Insert Voucher record\n5. VoucherController -> DbContext: SaveChangesAsync()\n6. VoucherController -> Browser: RedirectToAction(Index)"));

            // Sequence Diagram 2.5: Admin Revenue Report & CSV Export Download (II.25 - UC-30)
            InsertAfterCurrent(CreateParagraph("2.5 Sequence Diagram: Admin Revenue Report & CSV Export Download (II.25 - UC-30)", "Heading3", true));
            InsertAfterCurrent(CreateParagraph("Auditing report generation and CSV dataset download (Tải báo cáo của admin):\n1. Admin -> AdminController: HTTP GET /Admin/Report?from=YYYY-MM-DD&to=YYYY-MM-DD\n2. AdminController -> DbContext: Query Orders where CreatedAt >= start and CreatedAt < end\n3. AdminController: Aggregate OrderCount, CompletedOrderCount, CancelledOrderCount, Revenue, and NewUserCount\n4. AdminController -> Browser: Return View(AdminReportViewModel)\n5. Admin -> AdminController: HTTP GET /Admin/ExportReport?from=YYYY-MM-DD&to=YYYY-MM-DD\n6. AdminController -> DbContext: Query Orders with AsNoTracking() ordered by CreatedAt\n7. AdminController: Build StringBuilder CSV with header 'OrderCode,Channel,Status,PaymentStatus,TotalAmount,CreatedAt'\n8. AdminController: For each order, append escaped CSV row\n9. AdminController -> Browser: Return File(Encoding.UTF8.GetBytes(csv), 'text/csv', 'bms-report-YYYYMMDD-YYYYMMDD.csv')\n10. Browser: Triggers file download dialog for Admin"));
        }

        doc.Save(documentXmlPath);

        string tempRepack = Path.Combine(Path.GetTempPath(), "sds_repack_" + Guid.NewGuid().ToString("N") + ".docx");
        ZipFile.CreateFromDirectory(tempFolder, tempRepack);
        File.Copy(tempRepack, docxPath, overwrite: true);
        File.Delete(tempRepack);
        Directory.Delete(tempFolder, recursive: true);
        Console.WriteLine($"Report 3 - SDS.docx successfully saved ({new FileInfo(docxPath).Length} bytes).");
    }

    #endregion

    #region Update Markdown Files

    static void UpdateSrsMarkdown(string mdPath)
    {
        string content = """
# Software Requirements Specification (SRS)
## Bakery Management System (BMS)

**Version:** 3.0  
**Target platform:** ASP.NET Core 8 MVC, Razor Views, Entity Framework Core 8, SQL Server  
**Document status:** Updated baseline matching Tracking Sheet 1 (UC-01 to UC-40) & Sheet 2 (II.1 to II.39)  
**Language:** English  

---

## 1. Change Log

| Date | Action | In charge | Change Description |
|---|:---:|---|---|
| 13/09/2026 | A | TinDLB | Initial SRS baseline creation. |
| 14/09/2026 | A | ThaiTQ | Add Cashier initial profile and ordering requirements. |
| 14/09/2026 | A | DatPM | Add Baker production management workflow. |
| 12/10/2026 | M | TinDLB | Updated schema, 24 tables, and permission matrix. |
| 09/11/2026 | M | TinDLB, ThaiTQ | **Iteration 3 Update:** Aligned numbering with Tracking Sheet 2 (II.9 Voucher, II.10 Cashier Dashboard, II.11 POS Counter, II.19 Stock Alerts, II.20 Staff, II.21 Order Monitoring, II.22 Admin Dashboard, II.23 Production Tasks Edit/Cancel, II.24 Notifications, II.25 Revenue Report with CSV Export, II.26 Invoices with HTML Download, II.27 Online Orders, II.28 Order Status Update with Customer Notification, II.29 Delivery Assignment with Customer Notification); updated Table 4.2 with all 40 Use Cases UC-01 to UC-40. |

---

## 2. Overall Description

### 2.1 Actors
1. **Admin / Shop Owner:** Back-office administrator managing products, recipes, raw material inventory, procurement, staff accounts, promotions, production planning, order auditing, and management reporting.
2. **Cashier:** Sales and fulfillment operator handling counter POS sales, online order lifecycle management, delivery staff assignment, and invoice generation.
3. **Baker:** Production kitchen staff receiving assigned baking tasks, recording ingredient usage, entering actual yield and defect quantities.
4. **Customer:** Registered member purchasing online, managing addresses, applying vouchers, tracking order fulfillment, and receiving real-time notifications.
5. **Guest:** Anonymous visitor browsing the product catalog.

*(Suppliers and Delivery Staff are operational data entities; they do not possess application user accounts).*

---

## 3. User Requirements (Section 4.2 - Complete Use Case Inventory UC-01 to UC-40)

| ID | Use Case | Feature | Use Case Description |
|---|---|---|---|
| UC-01 | Login & Register | Authentication | User registers a new account and logs in with role-based access |
| UC-02 | Home Page & Product Detail | Online Shopping | Customer browses product list and views details of each item |
| UC-03 | Search & Filter Products | Online Shopping | Customer searches by name and filters by category or price |
| UC-04 | Shopping Cart | Online Shopping | Customer adds removes and adjusts quantity of items in cart |
| UC-05 | My Account & Address Management | Customer Profile | Customer updates personal info and manages multiple delivery addresses |
| UC-06 | Checkout & Online Payment | Online Shopping | Customer selects address applies voucher and completes COD or online payment |
| UC-07 | Order History & Tracking | Online Shopping | Customer views past orders and tracks current delivery status |
| UC-08 | User Notifications | Notification | Customer receives alerts when order is placed delivering and delivered |
| UC-09 | Staff Profile | Staff Management | Cashier views and edits own account info and changes password |
| UC-10 | Cashier Dashboard | Sales & POS | Cashier sees today total sales summary and count of pending orders |
| UC-11 | POS / Counter Order | Sales & POS | Cashier creates a walk-in order selects items and receives payment |
| UC-12 | Invoice Generation | Invoicing | System auto-generates, prints and downloads HTML invoice after order is completed |
| UC-13 | Voucher Management | Promotions | Admin creates edits and activates or deactivates voucher codes |
| UC-14 | Order Management Online | Order Fulfillment | Cashier views and filters the list of pending online orders |
| UC-15 | Order Status Update Pack and Prepare | Order Fulfillment | Cashier updates online order status from Pending to Preparing, Delivering, Completed, and notifies customer |
| UC-16 | Delivery & Shipper Management | Delivery Management | Cashier assigns Shipper info and marks order as Delivering or Completed, and notifies customer |
| UC-17 | Supplier Management | Supplier Management | Admin adds edits and views the list of ingredient suppliers |
| UC-18 | Goods Receipt Import Stock | Inventory Management | Admin creates a goods receipt to import ingredients from a supplier |
| UC-19 | Inventory Dashboard & Stock Alert | Inventory Management | Admin views current ingredient stock levels with low-stock warnings |
| UC-20 | Cost Report | Reporting | Admin views ingredient import cost report filtered by date range |
| UC-21 | Category & Product Management | Master Data Management | Admin manages product categories and product details name image price status |
| UC-22 | Recipe Management | Production Management | Admin manages production recipes linking products to ingredient quantities |
| UC-23 | Production Planning Task Creation | Production Management | Admin creates production tasks and system auto-calculates ingredient needs |
| UC-24 | Admin Stock Alert Notification | Notification | System sends alert to Admin when ingredient stock drops below minimum threshold |
| UC-25 | Ingredient Management | Inventory Management | Admin manages ingredient list with unit and minimum stock level setting |
| UC-26 | Baker Dashboard | Production Management | Baker views the full list of production tasks assigned by Admin |
| UC-27 | Production Execution & Yield Update | Production Management | Baker starts task system deducts ingredients and inputs actual yield produced |
| UC-28 | Production History | Production Management | Baker and Admin view completed production task history and yield vs plan |
| UC-29 | Loyalty Points | Customer Loyalty | Customer earns and redeems loyalty points during checkout |
| UC-30 | Revenue Report | Reporting | Admin views consolidated revenue report by date range and downloads CSV export |
| UC-31 | Purchase Order Management | Procurement | Admin creates and tracks purchase orders to suppliers; supports partial deliveries under the same PO code and PO cancellation |
| UC-32 | Goods Inspection & Quality Rejection | Quality Assurance | Admin inspects each delivered batch for quality and expiry, accepts or rejects defective items, and requests a replacement batch from the supplier |
| UC-33 | Manual Stock Adjustment | Inventory Management | Admin manually increases or decreases ingredient stock with a recorded reason such as correction, found stock, damage, expiry, or wastage |
| UC-34 | Stock Count & Reconciliation | Inventory Management | Admin performs a physical stock count and reconciles any variance against system quantities |
| UC-35 | Staff Account Management | Staff Management | Admin creates Baker and Cashier accounts, resets passwords and activates or deactivates staff accounts |
| UC-36 | Finished Product Stock Monitoring | Inventory Management | Admin views finished cake stock per product variant against threshold together with recent sales and sees a low stock flag to decide whether to produce |
| UC-37 | Production Task Management (Edit / Reassign / Cancel) | Production Management | Admin views and filters production tasks and edits, reassigns or cancels a Planned task; system updates the task and notifies the Baker |
| UC-38 | Production Notifications | Production Management | System notifies Baker when a task is assigned, updated or cancelled and notifies Admin when a task is completed or blocked from starting |
| UC-39 | Admin Dashboard | Reporting & Monitoring | Admin sees a summary of today orders, revenue, low-stock items and production status on one overview page |
| UC-40 | Order Monitoring (Admin) | Order Management | Admin views and filters all online and counter orders with status and payment information in read-only mode |

---

## 4. Functional Requirements (Section II Numbered Strictly by Tracking Sheet 2)

### II.2 Cashier Profile (UC-09 - ThaiTQ)
Allows the Cashier to view and edit personal information (FullName, Phone) and change their login password.

### II.9 Voucher Management (UC-13 - TinDLB)
- **Actor:** Admin
- **Description:** Admin configures promotional discount vouchers (percentage or fixed amount, minimum order value, max discount cap, date range, usage limit, active status).

### II.10 Cashier Dashboard (UC-10 - ThaiTQ)
- **Actor:** Cashier
- **Description:** Cashier home screen displaying today's total revenue and live count of orders in Pending, Preparing, Delivering, and Completed states.

### II.11 POS / Counter Order (UC-11 - ThaiTQ)
- **Actor:** Cashier
- **Description:** Allows Cashier to process direct walk-in sales with product selection, voucher redemption, strict Cash payment, automatic inventory deduction, and auto-generated invoice.

### II.19 Admin Stock Alert Notification (UC-24 - TinDLB)
- **Actor:** System / Admin
- **Description:** System detects when ingredient stock drops below `MinThreshold` and automatically delivers high-priority alerts to the Admin notification inbox.

### II.20 Staff Account Management (UC-35 - TinDLB)
- **Actor:** Admin
- **Description:** Admin creates, edits, and locks/unlocks Cashier and Baker staff accounts with BCrypt password hashing.

### II.21 Order Monitoring (Admin) (UC-40 - TinDLB)
- **Actor:** Admin
- **Description:** Centralized read-only monitoring interface displaying all online and offline orders across the bakery with multi-criteria filtering.

### II.22 Admin Dashboard (UC-39 - TinDLB)
- **Actor:** Admin
- **Description:** Real-time operational overview with KPI summary cards (revenue, orders, active users), inventory warnings, and recent activity.

### II.23 Production Task Management (Edit / Reassign / Cancel) (UC-37 - TinDLB)
- **Actor:** Admin
- **Description:** Admin edits planned quantities, reassigns bakers, or cancels planned production tasks with automated ingredient inventory recalculation.

### II.24 Production Notifications (UC-38 - TinDLB)
- **Actor:** Baker / Admin
- **Description:** Event-driven notification system sending task assignments, modifications, and cancellations to Bakers, and task start/completion events to Admins.

### II.25 Revenue Report & CSV Export Download (UC-30 - TinDLB)
- **Actor:** Admin
- **Description:** Consolidates revenue, order volumes, and new registrations across custom date ranges, and provides single-click **CSV report download (`bms-report-*.csv`)** for administrative reconciliation (Tải báo cáo của admin).

### II.26 Invoice Generation (UC-12 - ThaiTQ)
- **Actor:** Cashier
- **Description:** Issues unique sales invoice for completed and paid orders, providing in-browser viewing, printing, and direct **HTML download** containing customer, address, payment, and delivery staff details.

### II.27 Order Management Online List & Filter (UC-14 - ThaiTQ)
- **Actor:** Cashier
- **Description:** Tabbed order fulfillment queue allowing Cashier to search and filter online orders by status.

### II.28 Order Status Update: Pack & Prepare (UC-15 - ThaiTQ)
- **Actor:** Cashier
- **Description:** Transitions online orders across fulfillment lifecycle: `Pending -> Preparing -> Delivering -> Completed` (or `Cancelled`), and **automatically sends real-time notifications to the customer upon status change** (Thông báo cho người dùng khi cập nhật đơn từ cashier).

### II.29 Delivery & Fulfillment Management (Self-Delivery Staff) (UC-16 - ThaiTQ)
- **Actor:** Cashier
- **Description:** Assigns internal delivery personnel (name and 10-digit phone number) to orders in `Delivering` status, and **automatically sends shipper details to the customer via notification** (Thông báo cho người dùng khi phân công shipper).
""";

        File.WriteAllText(mdPath, content, Encoding.UTF8);
        Console.WriteLine($"SRS.md successfully updated ({new FileInfo(mdPath).Length} bytes).");
    }

    static void UpdateSdsMarkdown(string mdPath)
    {
        string content = """
# Software Design Specification (SDS)
## Bakery Management System (BMS)

**Version:** 3.0  
**Target platform:** ASP.NET Core 8 MVC, Razor Views, Entity Framework Core 8, SQL Server  
**Database:** `BakeryManagementDB`  
**Document status:** Updated Detailed Code Design matching Tracking Sheet 2  
**Language:** English  

---

## 1. Change Log

| Date | Action | In charge | Change Description |
|---|:---:|---|---|
| 01/10/2026 | A | TinDLB | Initial SDS baseline creation. |
| 12/10/2026 | M | TinDLB | Updated High Level Architecture and Database Design for 24 tables. |
| 09/11/2026 | M | TinDLB, ThaiTQ | **Iteration 3 Update:** Structured Section II Detailed Code Design strictly matching Tracking Sheet 2: Feature Set 1 (Cashier Sales, Fulfillment, Invoicing & Customer Notification by ThaiTQ - II.2, II.10, II.11, II.26, II.27, II.28, II.29) and Feature Set 2 (Admin Operations, Production Planning & Reassignment, Staff Management, Voucher Management, and Revenue Report with CSV Export by TinDLB - II.3, II.7, II.8, II.9, II.19, II.20, II.21, II.22, II.23, II.24, II.25) with Class Diagrams and Sequence Diagrams. |

---

## 2. High Level Architecture

BMS follows a layered ASP.NET Core MVC architecture:

```text
+-------------------------------------------------------------+
|                        Client Tier                          |
|         Web Browser / Razor Views / Bootstrap UI            |
+-------------------------------------------------------------+
                              | HTTP(S) / Anti-Forgery Token
                              v
+-------------------------------------------------------------+
|                      Presentation Tier                      |
|  MVC Controllers (Admin, Cashier, Baker, Order, Checkout)   |
|  Dedicated Form & Display ViewModels (DataAnnotations)      |
+-------------------------------------------------------------+
                              | Dependency Injection
                              v
+-------------------------------------------------------------+
|                      Application Tier                       |
|  Business Services: NotificationService, BakerService,      |
|  IngredientService, VoucherService, PasswordService         |
+-------------------------------------------------------------+
                              | Unit of Work / Transactions
                              v
+-------------------------------------------------------------+
|                      Data Access Tier                       |
|   BakeryManagementDbContext (Entity Framework Core 8)       |
+-------------------------------------------------------------+
                              | SQL Provider / TDS
                              v
+-------------------------------------------------------------+
|                          Data Tier                          |
|         Microsoft SQL Server 2022 (BakeryManagementDB)      |
+-------------------------------------------------------------+
```

---

## 3. Detailed Code Design (Section II Numbered by Tracking Sheet 2)

### 3.1 Feature Set 1: Cashier Sales, Fulfillment, Invoicing & Customer Notification (ThaiTQ - II.2, II.10, II.11, II.26, II.27, II.28, II.29)

#### 3.1.1 Class Diagram - Cashier Module
- **`CashierController`**:
  - `+ Dashboard(): Task<IActionResult>` (II.10 - UC-10)
  - `+ CounterOrder(): Task<IActionResult>` (II.11 - UC-11)
  - `+ CounterOrder(CounterOrderViewModel): Task<IActionResult>` (II.11 - UC-11)
  - `+ Orders(string, string, string, int): Task<IActionResult>` (II.27 - UC-14)
  - `+ OrderDetails(int): Task<IActionResult>`
  - `+ UpdateOrderStatus(int, string): Task<IActionResult>` (II.28 - UC-15)
  - `+ AssignDelivery(int): Task<IActionResult>` (II.29 - UC-16)
  - `+ AssignDelivery(AssignDeliveryViewModel): Task<IActionResult>` (II.29 - UC-16)
  - `+ GenerateInvoice(int): Task<IActionResult>` (II.26 - UC-12)
  - `+ Invoice(int): Task<IActionResult>` (II.26 - UC-12)
  - `+ DownloadInvoice(int): Task<IActionResult>` (II.26 - UC-12)

#### 3.1.2 Sequence Diagram: POS Counter Order Checkout (II.11 - UC-11)
```text
Cashier          CashierController        DbContext            VoucherService        Database
   |                     |                    |                      |                  |
   |-- POST CounterOrder |                    |                      |                  |
   |-------------------->|                    |                      |                  |
   |                     |-- Validate Stock ->|                      |                  |
   |                     |                    |---------------------------------------->|
   |                     |-- Apply Voucher ------------------------->|                  |
   |                     |<-- Discount Amount -----------------------|                  |
   |                     |-- Begin Transaction----------------------------------------->|
   |                     |-- Decrement Stock ------------------------------------------>|
   |                     |-- Insert Order (Offline, Cash, Completed, Paid) ------------>|
   |                     |-- Insert OrderItems & Invoice ------------------------------>|
   |                     |-- Commit Transaction --------------------------------------->|
   |                     |                                                              |
   |<-- RedirectToInvoice|                                                              |
```

#### 3.1.3 Sequence Diagram: Online Order Lifecycle Update with Customer Notification (II.28 - UC-15)
```text
Cashier          CashierController        DbContext        NotificationService       Customer Inbox
   |                     |                    |                    |                       |
   |-- UpdateStatus(id) -|                    |                    |                       |
   |-------------------->|                    |                    |                       |
   |                     |-- Begin Serializable Tx                 |                       |
   |                     |-- Verify Valid Transition               |                       |
   |                     |-- Update Order.Status ->|               |                       |
   |                     |-- Commit Tx ------>|                    |                       |
   |                     |                                         |                       |
   |                     |-- NotifyAdminsAsync('OrderStatusChanged')                       |
   |                     |---------------------------------------->|                       |
   |                     |                                         |                       |
   |                     |-- CreateNotificationAsync(order.CustomerId,                     |
   |                     |   'OrderStatusChanged', 'Đơn hàng cập nhật')                    |
   |                     |---------------------------------------->|                       |
   |                     |                                         |-- Persist & Deliver ->|
   |<-- Redirect / Refresh                                                                 |
```

#### 3.1.4 Sequence Diagram: Delivery Staff Assignment & Customer Notification (II.29 - UC-16)
```text
Cashier          CashierController        DbContext        NotificationService       Customer
   |                     |                    |                    |                     |
   |-- POST AssignDelivery(name, phone)       |                    |                     |
   |-------------------->|                    |                    |                     |
   |                     |-- Validate Delivering Status            |                     |
   |                     |-- Save Staff Info->|                    |                     |
   |                     |                                         |                     |
   |                     |-- CreateNotificationAsync(order.CustomerId,                   |
   |                     |   'DeliveryAssigned', 'Người giao hàng: ' + name)             |
   |                     |---------------------------------------->|                     |
   |                     |                                         |-- Send to Customer >|
   |<-- Redirect OrderDetails                                                            |
```

#### 3.1.5 Sequence Diagram: Invoice Generation & HTML Export Download (II.26 - UC-12)
```text
Cashier          CashierController        DbContext                 Browser
   |                     |                    |                        |
   |-- DownloadInvoice --|                    |                        |
   |-------------------->|                    |                        |
   |                     |-- Query Order, Items, Address, Invoice ---->|
   |                     |-- Construct Formatted UTF-8 HTML            |
   |                     |-- Encode all values (HtmlEncode)            |
   |                     |-- Return File(bytes, 'text/html', filename) |
   |                     |-------------------------------------------->|
   |                     |                                             |-- Triggers Download
```

---

### 3.2 Feature Set 2: Admin Operations, Production Planning, Staff & Reporting (TinDLB - II.3, II.7, II.8, II.9, II.19, II.20, II.21, II.22, II.23, II.24, II.25)

#### 3.2.1 Class Diagram - Admin & Production Module
- **`AdminController`**:
  - `+ Dashboard(): Task<IActionResult>` (II.22 - UC-39)
  - `+ Orders(string, string, DateTime?, DateTime?, int): Task<IActionResult>` (II.21 - UC-40)
  - `+ Notifications(bool): Task<IActionResult>`
  - `+ Report(DateTime?, DateTime?): Task<IActionResult>` (II.25 - UC-30)
  - `+ ExportReport(DateTime?, DateTime?): Task<IActionResult>` (II.25 - UC-30)
- **`ProductionTaskController`**:
  - `+ Index(): Task<IActionResult>`
  - `+ Create(ProductionTaskViewModel): Task<IActionResult>` (II.8 - UC-23)
  - `+ Edit(int, ProductionTaskViewModel): Task<IActionResult>` (II.23 - UC-37)
  - `+ Cancel(int): Task<IActionResult>` (II.23 - UC-37)
- **`StaffController`**:
  - `+ Index(): Task<IActionResult>` (II.20 - UC-35)
  - `+ Create(StaffFormViewModel): Task<IActionResult>` (II.20 - UC-35)
  - `+ Edit(int, StaffFormViewModel): Task<IActionResult>` (II.20 - UC-35)
  - `+ ToggleStatus(int): Task<IActionResult>` (II.20 - UC-35)
- **`VoucherController`**:
  - `+ Index(): Task<IActionResult>` (II.9 - UC-13)
  - `+ Create(VoucherFormViewModel): Task<IActionResult>` (II.9 - UC-13)
  - `+ Edit(int, VoucherFormViewModel): Task<IActionResult>` (II.9 - UC-13)
  - `+ ToggleStatus(int): Task<IActionResult>` (II.9 - UC-13)

#### 3.2.2 Sequence Diagram: Staff Account Creation & Deactivation (II.20 - UC-35)
```text
Admin            StaffController          DbContext          NotificationService     SQL Database
  |                     |                     |                      |                    |
  |-- POST Create(model)|                     |                      |                    |
  |-------------------->|                     |                      |                    |
  |                     |-- Verify Unique Email                      |                    |
  |                     |-- Hash Password (BCrypt)                   |                    |
  |                     |-- Insert User (Cashier/Baker) ------------>|                    |
  |                     |-- SaveChangesAsync() ------------------------------------------>|
  |                     |-- NotifyAdminsAsync('StaffCreated') ------>|                    |
  |<-- RedirectToIndex -|                                                                 |
```

#### 3.2.3 Sequence Diagram: Production Task Management & Notifications (II.23 - UC-37, II.24 - UC-38, II.19 - UC-24)
```text
Admin        ProductionTaskController     DbContext          NotificationService     Baker Inbox
  |                     |                     |                      |                    |
  |-- POST Edit(model) -|                     |                      |                    |
  |-------------------->|                     |                      |                    |
  |                     |-- Verify Status != Completed               |                    |
  |                     |-- Check Ingredient Stock Requirements      |                    |
  |                     |-- Update TargetQuantity & AssignedBakerId ->|                   |
  |                     |-- SaveChangesAsync() ------------------------------------------>|
  |                     |-- CreateNotificationAsync(bakerId, 'TaskUpdated') ------------->|
  |                     |-- NotifyAdminsAsync('TaskUpdated') ------->|                    |
  |<-- RedirectToIndex -|                                                                 |
```

#### 3.2.4 Sequence Diagram: Admin Revenue Report & CSV Export Download (II.25 - UC-30)
```text
Admin             AdminController         DbContext                 Browser
  |                      |                    |                        |
  |-- GET Report(from,to)|                    |                        |
  |--------------------->|                    |                        |
  |                      |-- Query Orders & Users in Range             |
  |                      |-- Aggregate Revenue & Counts                |
  |<-- Return View(model)|                                             |
  |                                                                    |
  |-- GET ExportReport --|                                             |
  |--------------------->|                                             |
  |                      |-- Query Orders AsNoTracking() ------------->|
  |                      |-- Build CSV (StringBuilder)                 |
  |                      |-- Return File(bytes, 'text/csv', filename) ->|
  |                      |                                             |-- Triggers Download
```
""";

        File.WriteAllText(mdPath, content, Encoding.UTF8);
        Console.WriteLine($"SDS.md successfully updated ({new FileInfo(mdPath).Length} bytes).");
    }

    #endregion
}

# Bakery Management System (BMS)

## Updated Project Specification & Team Development Plan

This document is the current baseline after mentor feedback. Team
members should use it consistently for requirements, diagrams, ERD,
implementation, and testing.

## 1. Project Overview

The Bakery Management System (BMS) is a centralized system for managing
the daily operations of a small bakery. It connects product management,
ingredient inventory, recipes, production, suppliers, contacts, sales,
payments, delivery, promotions, loyalty points, and reports.

### Actors

-   **Admin / Shop Owner** -- manages and monitors the bakery's
    operations.
-   **Baker** -- handles assigned production tasks and records
    production results.
-   **Cashier** -- handles sales operations, payments, invoices, orders,
    and delivery status.
-   **Customer** -- uses a registered account to purchase products and
    access account-related information.
-   **Guest** -- browses products and may use guest checkout if
    implemented.
-   **Payment Gateway (Mock)** -- simulates online payment.

**Supplier is NOT an actor.** Suppliers provide ingredients in real
life, but they do not directly use the software. The Admin manages
supplier information and receiving inventory.

------------------------------------------------------------------------

## 2. Main Modules

1.  **User & Authentication Management**
    -   Login/logout
    -   User accounts
    -   Role-based access
    -   Customer profile
    -   User-linked loyalty points
2.  **Product Management**
    -   Categories
    -   Products
    -   Selling prices
    -   Product availability
    -   Search/filter
3.  **Ingredient & Inventory Management**
    -   Ingredients
    -   Current stock
    -   Minimum stock level
    -   Receiving ingredients
    -   Stock adjustment
    -   Inventory transactions
    -   Low-stock notifications
    -   Ingredient availability checking
    -   Finished-product stock
4.  **Recipe Management**
    -   Create/update/view/delete recipes
    -   Recipe ingredients and quantities
    -   Baker can view recipes but cannot edit them
5.  **Production Management**
    -   Create production tasks
    -   Assign Baker
    -   Calculate required ingredients
    -   Check available ingredients
    -   Adjust quantity when ingredients are insufficient
    -   Start production
    -   Enter actual quantity
    -   Complete production
    -   Production history
6.  **Supplier Management**
    -   Supplier information
    -   Contact person
    -   Phone
    -   Address
    -   Tax code
    -   Supplied ingredients
    -   Supplier status
7.  **Contact Management**
    -   Replaces Customer Management
    -   Supports Individual and Organization contacts
    -   Individual: name, phone, address
    -   Organization: contact person, company name, tax code, phone,
        address
8.  **Sales Management**
    -   Shopping Cart
    -   Orders
    -   Payment
    -   Invoice
    -   Delivery
    -   Voucher
    -   Loyalty Point
9.  **Reports & Dashboard**
    -   Revenue
    -   Order count
    -   Best sellers
    -   Inventory status
    -   Production history
    -   Planned vs actual production
    -   Ingredient cost
    -   Estimated profit

------------------------------------------------------------------------

## 3. Contact, User, Supplier and Loyalty Point

These concepts must remain separate.

### User

Represents a system account: - UserID - Username - Password - Role -
Status - Loyalty Point balance/history

### Contact

Represents personal or organizational contact information.

**Individual** - Name - Phone - Address

**Organization** - Contact person - Company name - Tax code - Phone -
Address

### Supplier

Represents a provider of bakery ingredients. Supplier does not directly
log into the system.

### Loyalty Point

Loyalty points belong to the **User account**, not to Contact.

``` text
User
  |
  +-- Loyalty Points

Contact
  |
  +-- Individual / Organization information

Supplier
  |
  +-- Supplier information
```

------------------------------------------------------------------------

## 4. Inventory Design

The system tracks two important stock types.

### Ingredient Inventory

Examples: - Flour - Sugar - Butter - Milk - Eggs - Chocolate

Each ingredient should have: - IngredientID - IngredientName - Unit -
CurrentQuantity - MinimumStockLevel - Status

### Finished Product Inventory

Examples: - Croissant - Cake - Bread - Cookies

Production increases finished-product stock. Sales decrease
finished-product stock.

### Inventory Flow

``` text
Supplier
   |
   | provides ingredients
   v
Admin
   |
   | receive ingredients
   v
Ingredient Inventory
   |
   v
Production
   |
   +--> deduct ingredients
   |
   +--> increase finished-product stock
                  |
                  v
                Sales
                  |
                  +--> decrease finished-product stock
```

### Low Stock Rule

If:

`Current Quantity < Minimum Stock Level`

the system creates a low-stock notification for Admin.

------------------------------------------------------------------------

## 5. Production Management

### Main Flow

``` text
Admin creates Production Task
        |
        v
Select Product + Planned Quantity
        |
        v
System retrieves Recipe
        |
        v
Calculate Required Ingredients
        |
        v
Check Ingredient Inventory
        |
        +--> insufficient --> calculate maximum feasible quantity
        |
        v
Assign Baker
        |
        v
Baker views task
        |
        v
Start Production
        |
        v
Enter Actual Quantity
        |
        v
Complete Production
        |
        v
System updates inventory
```

### Insufficient Ingredient Rule

Example:

``` text
Requested: 50 Croissants

Flour capacity  = 75
Butter capacity = 30
Milk capacity   = 100

Maximum feasible quantity = 30
```

The system warns Admin and adjusts the planned quantity to 30.

### Actual Quantity Rule

If:

``` text
Planned = 30
Actual  = 28
```

after completion: - ingredients are deducted according to actual
production; - finished-product stock increases by 28; - production
status becomes Completed.

The Baker does not manually edit finished-product stock.

------------------------------------------------------------------------

## 6. Sales Management

Shopping Cart, Order, Payment, Invoice, Delivery, Voucher, and Loyalty
Point are grouped into the **Sales Management** module.

### Sales Flow

``` text
Product
   |
Shopping Cart
   |
Order
   |
Voucher / Loyalty Point
   |
Payment
   |
Invoice
   |
Delivery
   |
Order Completed
```

### Shopping Cart

-   Add product
-   View cart
-   Update quantity
-   Remove product
-   Calculate subtotal

### Order

-   Create order
-   View details
-   Update status
-   Counter order
-   Online order
-   Order history

Suggested statuses:

`Pending -> Confirmed -> Preparing -> Ready -> Completed`

Delivery can use:

`Ready -> Delivering -> Delivered`

### Payment

-   Cash for counter orders
-   Mock online payment for online orders
-   No real VNPay/MoMo/Stripe integration

### Delivery

The bakery uses self-delivery. - No Shipper actor - No separate Delivery
Service actor - Store delivery address, recipient information, notes,
and status

### Voucher

-   Create/manage voucher
-   Validity period
-   Minimum order
-   Usage limit
-   Discount
-   Validate/apply voucher

### Loyalty Point

-   Earn points from completed orders
-   Redeem points
-   View balance
-   View point history
-   Points are linked to User

------------------------------------------------------------------------

## 7. Recommended Admin Use Cases

-   Login
-   Manage Categories
-   Manage Products
-   Manage Ingredients & Inventory
-   Manage Recipes
-   Manage Production Tasks
-   Assign Baker
-   Receive Low Stock Notifications
-   Manage Suppliers
-   Manage Contacts
-   Manage Vouchers / Promotions
-   Manage Staff
-   View Orders / Sales Information
-   View Reports & Dashboard
-   View Revenue
-   View Production History

Recommended relationships:

``` text
Manage Production Tasks
        <<include>>
             |
        Assign Baker
```

``` text
Manage Ingredients & Inventory
        <<extend>>
             |
Receive Low Stock Notification
[Stock < Minimum Stock Level]
```

Login should connect directly to Admin rather than being included in
every administrative use case.

------------------------------------------------------------------------

## 8. Recommended Baker Use Cases

-   Login
-   View Recipes
-   View Assigned Production Tasks
-   View Production Task Details
-   View Required Ingredients
-   Start Production
-   Enter Actual Quantity
-   Complete Production

Recommended relationship:

``` text
View Production Task Details
        <<include>>
             |
   View Required Ingredients
```

Baker cannot: - Create production tasks - Assign production tasks -
Modify official recipes - Manually edit inventory

------------------------------------------------------------------------

## 9. Cashier Responsibilities

-   Login
-   Create Counter Order
-   Process Online Orders
-   View Order Details
-   Process Payment
-   Generate/View Invoice
-   Apply Voucher
-   Update Order Status
-   Update Delivery Status
-   View required contact information

Cashier does not manage recipes, production planning, suppliers, or
staff accounts.

------------------------------------------------------------------------

## 10. Customer Responsibilities

Registered Customers can: - Login/logout - Manage profile - Browse
products - View product details - Add/update/remove cart items -
Checkout - Place orders - Provide delivery information - Make payment -
Apply voucher - Use loyalty points - View order history - View order
status - View invoices

------------------------------------------------------------------------

## 11. Guest Responsibilities

Guest can: - Browse products - Search/filter products - View product
details - View availability - View promotions - Use cart and checkout if
guest checkout is enabled

Guest checkout may collect: - Name - Phone - Delivery address - Delivery
note - Payment information

Guest does not have an account or account-linked loyalty points.

------------------------------------------------------------------------

## 12. Core Entities

-   User
-   Contact
-   Supplier
-   Category
-   Product
-   Ingredient
-   Recipe
-   RecipeIngredient
-   ProductionTask
-   Order
-   OrderDetail
-   Payment
-   Invoice
-   Voucher
-   LoyaltyPoint
-   InventoryTransaction

### Important relationships

``` text
Product 1 --- 1 Recipe
Recipe 1 --- * RecipeIngredient
Ingredient 1 --- * RecipeIngredient

Product 1 --- * ProductionTask
User/Baker 1 --- * ProductionTask

Order 1 --- * OrderDetail
Product 1 --- * OrderDetail

User 1 --- * LoyaltyPoint
```

The exact Contact-Supplier and User-Contact relationships should be
finalized in the ERD before implementation.

------------------------------------------------------------------------

## 13. Context Diagram Rules

Central system:

**Bakery Management System**

External entities: - Admin - Baker - Cashier - Customer - Guest -
Payment Gateway (Mock)

Do not include Supplier as an actor because Supplier does not directly
interact with the system.

Do not include Shipper or Delivery Service because the bakery performs
self-delivery.

Arrows must represent **DATA FLOWS**, not functions.

Good examples: - Product Information - Order Details - Payment
Information - Payment Result - Recipe Data - Production Task Data -
Inventory Status - Delivery Information - Customer Information -
Reports - Notifications

Avoid: - Manage Products - Process Orders - Create Production Task -
Manage Inventory

------------------------------------------------------------------------

## 14. Project Limitations

### L01 -- Real Payment Gateway

Online payment is simulated through a Mock Payment Gateway.

### L02 -- Self Delivery

The bakery handles delivery itself. No external delivery company or
Shipper account.

### L03 -- Supplier Interaction

Suppliers do not directly access the system. Admin manages supplier
information and ingredient receiving.

### L04 -- Single Bakery

The system targets one small bakery rather than a multi-branch
enterprise.

### L05 -- Basic Reports

Reports are limited to core operational and business information
required by the project.

------------------------------------------------------------------------

# 15. Updated 3-Sprint Plan

## Sprint 1 -- Foundation & Master Data

### Member 1 -- User & Contact

-   Authentication
-   Login/logout
-   Role-based access
-   User foundation
-   Customer/User profile
-   Contact Management
-   Individual/Organization contact

### Member 2 -- Product

-   Category Management
-   Product Management
-   Product listing/detail
-   Search/filter
-   Availability

### Member 3 -- Ingredient, Inventory & Supplier

-   Ingredient Management
-   Inventory structure
-   Current quantity
-   Minimum stock
-   Inventory transaction structure
-   Supplier Management
-   Supplier-ingredient relationship

### Member 4 -- Recipe & System Foundation

-   Recipe Management
-   RecipeIngredient
-   ERD/database schema
-   Models/entities
-   DAO/repository foundation
-   Shared UI/layout
-   Validation/error handling

------------------------------------------------------------------------

## Sprint 2 -- Core Operations

### Member 1 -- Sales: Cart & Orders

-   Shopping Cart
-   Checkout
-   Online orders
-   Counter-order foundation
-   Order details
-   Order history
-   Guest checkout if enabled

### Member 2 -- Sales: Payment, Invoice & Delivery

-   Payment
-   Mock Payment Gateway
-   Invoice
-   Order status
-   Delivery information
-   Delivery status
-   Sales integration

### Member 3 -- Production Management

-   Production Task
-   Create task
-   Planned quantity
-   Recipe-based ingredient calculation
-   Availability checking
-   Maximum feasible quantity
-   Automatic quantity adjustment
-   Assign Baker
-   Baker task list
-   Start production
-   Actual quantity
-   Complete production

### Member 4 -- Inventory Automation & Integration

-   Deduct ingredients after production
-   Add finished-product stock
-   Deduct finished-product stock after sales
-   Low-stock checking
-   Notifications
-   Production/inventory integration
-   Sales/inventory integration
-   Integration testing

------------------------------------------------------------------------

## Sprint 3 -- Sales Features, Reports & Final Integration

### Member 1 -- Voucher & Loyalty

-   Voucher Management
-   Voucher validation
-   Discount calculation
-   Loyalty point earning
-   Loyalty point redemption
-   Loyalty point history
-   User-linked loyalty balance

### Member 2 -- Cashier & Sales Operations

-   Counter sales
-   Online order processing
-   Contact information
-   Payment/invoice integration
-   Delivery status
-   Sales workflow refinement

### Member 3 -- Admin & Management

-   Staff Management
-   Admin dashboard
-   Order/sales monitoring
-   Inventory monitoring
-   Production monitoring
-   Notifications
-   Administrative UI refinement

### Member 4 -- Reports, QA & Final Integration

-   Revenue reports
-   Production history
-   Best-selling products
-   Inventory reports
-   Ingredient cost
-   Estimated profit
-   System integration
-   Bug fixing
-   Final testing
-   Demo preparation

------------------------------------------------------------------------

# 16. Final Overall Business Flow

``` text
Supplier
   |
   | provides ingredients
   v
Admin
   |
   +--> Manage Supplier
   |
   +--> Receive Ingredients
   |
   v
Ingredient Inventory
   |
   v
Recipe
   |
   v
Production Task
   |
   v
Assign Baker
   |
   v
Baker Produces
   |
   v
Actual Production Quantity
   |
   +--> Deduct Ingredient Stock
   |
   +--> Increase Finished Product Stock
                 |
                 v
               Sales
                 |
          +------+------+
          |             |
     Shopping Cart   Counter Order
          |             |
          +------+------+
                 |
          Voucher / Loyalty
                 |
              Payment
                 |
              Invoice
                 |
              Delivery
                 |
           Order Completed
```

## 17. Key Decisions

1.  Shopping Cart, Delivery, Voucher, and Loyalty Point are part of
    **Sales Management**.
2.  **Supplier Management** is a separate module.
3.  Supplier is **not an actor** because suppliers do not directly use
    the system.
4.  Customer Management is replaced by **Contact Management**.
5.  Contact supports **Individual** and **Organization**.
6.  Organization Contact includes **Company Name** and **Tax Code**.
7.  Loyalty Points belong to **User**, not Contact.
8.  Ingredient Inventory and Finished Product Inventory are both
    tracked.
9.  Production connects Recipe, Ingredient Inventory, and Finished
    Product Inventory.
10. Insufficient ingredients cause the system to calculate and adjust
    the feasible production quantity.
11. Baker records actual production quantity; the system updates
    inventory.
12. Bakery uses self-delivery; no Shipper actor.
13. Online payment uses a Mock Payment Gateway.
14. Context Diagram arrows represent data flows, not functions.
15. Use Case Diagrams show actor interactions and use cases, not
    internal database details.
16. All diagrams and implementation should use the same terminology and
    business rules in this document.

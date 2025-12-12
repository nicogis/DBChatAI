# DBChatAI


# AI-powered SQL Chat for DevExpress XAF (Blazor)

An AI-driven natural language chat that allows users to query SQL Server databases inside a **DevExpress XAF Blazor** application.  
The system combines **Azure OpenAI** with a **local SQL command engine** to provide safe, predictable, and efficient database querying.

---

## 🚀 Overview

This project demonstrates how to build a **natural-language database chat** integrated into DevExpress XAF.

Users can ask questions like:

- “List all employees hired after 2010”
- “Sort them by BirthDate”
- “Next 20”
- “Filter by JobTitle = 'Manager'”

without writing SQL manually.

The AI is used **only to generate the initial SQL query**.  
All subsequent commands (paging, sorting, filtering) are handled **locally**, without calling the AI again.

This guarantees:
- predictable behavior
- lower AI costs
- better performance
- safer SQL execution

---

## 🧠 Architecture

User (Chat UI)

↓

DevExpress AI Chat (Blazor)

↓

Command Detection Engine

├─ Local command? → SQL patch (paging / sort / filter)

└─ New question? → Azure OpenAI

↓

SQL generation

↓

Safe SQL execution

↓

Markdown table result



### Design principles

- **AI for intent, not execution**
- **SQL execution is always local**
- **No DML allowed (SELECT-only)**
- **Row limits enforced server-side**
- **Local commands do not call AI**

---

## ✨ Features

### AI & Chat
- Natural language → SQL generation using **Azure OpenAI**
- Integrated with **DevExpress Blazor AI Chat**
- Chat sessions persisted with XAF business objects

### SQL Safety
- SELECT-only execution
- Maximum result rows enforced (`MaxResultRows`)
- Automatic ORDER BY / OFFSET / FETCH validation
- No direct user SQL execution

### Intelligent Local Commands (No AI calls)
- Paging:
  - `next`, `previous`
  - `next 20`, `previous 10`
  - `page 3`, `first page`
- Sorting:
  - `sort by Name`
  - `sort them by Birth date`
  - tolerant to natural language
- Filtering:
  - `filter by HireDate > 2010`
  - paging automatically resets after filtering
- Reset commands:
  - `clear filters`
  - `clear sorting`
  - `reset all`

### UI
- Results rendered as **Markdown tables**
- Clean chat-based interaction
- Fully embedded in XAF Blazor UI

---

## 🏗️ Technologies Used

- DevExpress XAF Blazor
- DevExpress AI Chat component
- Azure OpenAI
- SQL Server
- .NET / C#
- Markdig (Markdown rendering)

---

📊 Sample Database

This project uses the Microsoft AdventureWorks sample database for demonstration and testing purposes.

AdventureWorks is an official Microsoft sample database designed to showcase SQL Server features and realistic business schemas (Human Resources, Sales, Production, Purchasing, etc.).

Download

You can download the AdventureWorks sample database directly from Microsoft:

👉 https://learn.microsoft.com/en-us/sql/samples/adventureworks-install-configure

Available versions include:

- OLTP

- Data Warehouse

- Lightweight (LT)

For this project, the OLTP version is recommended.

Notes

AdventureWorks is used only as a sample dataset

The project is database-agnostic and can be adapted to:

- custom schemas

- legacy databases

- reporting databases

Column and table names in examples reflect the AdventureWorks schema

## 🔐 Security Considerations

- Only **SELECT** queries are allowed
- SQL is generated and validated server-side
- Row limits are enforced independently of SQL text
- No user-provided SQL is executed directly
- Designed to integrate with the XAF Security System

---

## 🔐 Additional Security Recommendations (Read-Only Access)

Even though all generated SQL queries are strictly **sanitized and limited to SELECT statements**, it is **strongly recommended** to enforce database-level safety as an additional protection layer.

### Recommended setup

- Use a **dedicated database user with read-only permissions**
  - No INSERT / UPDATE / DELETE
  - No schema modification rights
  - Access limited only to required tables or views

- Preferably connect to:
  - a **read-only replica** of the production database, or
  - a **separate reporting / read-only instance**

This ensures that:
- accidental or unexpected queries cannot modify production data
- performance impact on the primary database is minimized
- security does not rely solely on application-level safeguards

### Defense in depth

The system follows a layered security approach:

1. AI prompt constraints (SELECT-only intent)
2. SQL validation and execution rules
3. Server-side row limits
4. Local command engine (no arbitrary SQL regeneration)
5. **Database-level read-only access (recommended)**

Using a read-only database user or a read-only replica is considered a best practice for production deployments.

---

## ⚙️ Configuration

### Azure OpenAI

You need:
- An Azure OpenAI resource
- A deployed model (e.g. GPT-4 / GPT-4o / GPT-4.1)
- API key and endpoint

These are injected into the AI client used by the chat service.

### SQL Execution

- `MaxResultRows` must be defined consistently
- The same value is used by:
  - the SQL executor
  - the local paging logic

---

## 🧪 Example Flow

1. User:
   > List all employees hired after 2010

2. AI generates:
   ```sql
   SELECT BusinessEntityID, JobTitle, HireDate
   FROM HumanResources.Employee
   WHERE HireDate > '2010-12-31'

3. SQL executor returns the first 100 rows (max limit)

4. User:
Sort them by BirthDate

5. Local engine patches SQL:

SELECT BusinessEntityID, JobTitle, HireDate
FROM HumanResources.Employee
WHERE HireDate > '2010-12-31'
ORDER BY BirthDate


6. No additional AI call required ✅


📌 Project Status

This project is intended as:

✅ a real-world reference implementation

✅ a starting point for enterprise projects

❌ not a plug-and-play product

You are expected to adapt:

- prompts

- security rules

- column mappings

- UI behavior

- to your own application needs.

🛠️ Possible Extensions

- Column name auto-discovery from database metadata

- Smarter natural-language column mapping

- Role-based query restrictions

- Query explanation mode

- Result export (Excel / CSV)

- Audit logging


🙌 Acknowledgements

- DevExpress XAF & Blazor

- Azure OpenAI

- Markdig

![DBChatAI Demo](Images/DBChatAI.gif)

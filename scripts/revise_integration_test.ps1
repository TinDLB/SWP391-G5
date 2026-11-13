$ErrorActionPreference='Stop'
$root='D:\PRN232\PRN232_SE1934\a\SWP391-G5'
$source='D:\SWP391-G5-Test\info\Integration-Test.xlsx'
$out=Join-Path $root 'Test-Documents\Integration-Test-TinDLB.xlsx'
New-Item (Split-Path $out) -ItemType Directory -Force | Out-Null
Copy-Item $source $out -Force
$excel=New-Object -ComObject Excel.Application
$excel.Visible=$false
$excel.DisplayAlerts=$false
$book=$excel.Workbooks.Open($out)
function SetCell($sheet,$cell,$value){$sheet.Range($cell).Value2=[string]$value}
$modules=@(
 @('UC-37','Production Task Management','Create, edit, cancel and query production tasks.','Admin and database test data exist'),
 @('UC-38','Production Notifications','Persist task assignment, update, cancellation and shortage notifications.','Notification service configured'),
 @('UC-35','Staff Account Management','Validate and persist staff account changes.','Admin authenticated'),
 @('UC-39','Admin Dashboard','Aggregate operational data for Admin dashboard.','Representative data exists'),
 @('UC-40','Order Monitoring Admin','Query and filter customer orders.','Orders exist'),
 @('UC-30','Revenue Report','Calculate and export revenue data.','Completed orders exist'),
 @('UC-24','Admin Stock Alert Notification','Detect low-stock ingredients and notify Admin.','Ingredient thresholds configured'))
$cases=@(
 @('IT-UC37-001','Create and assign production task','Admin creates a task with active product/recipe, quantity, Baker and future deadline.','Task is Assigned; recipe ingredients and calculated quantities are saved; Baker and Admin receive notifications.','Admin authenticated; active recipe and Baker exist.','UC-37'),
 @('IT-UC37-002','Reject invalid production task','Submit with no active recipe, invalid Baker or past deadline.','Validation is displayed and no task or notification is inserted.','Invalid test data exists.','UC-37'),
 @('IT-UC37-003','Edit, recalculate and cancel task','Edit task details, verify ingredient recalculation, then cancel a cancellable task.','Task ingredients are recalculated; reassignment/cancellation notifications are created; completed task cannot be changed.','Editable and completed tasks exist.','UC-37'),
 @('IT-UC38-001','Persist task notifications','Create, edit, reassign and cancel a task, then query notifications.','Recipient, type, title, message and timestamp are stored correctly.','Notification service and database are available.','UC-38'),
 @('IT-UC38-002','Persist shortage notifications','Create a task requiring more ingredients than available stock.','Task remains created; shortage details are sent to Admin and Baker.','Insufficient ingredient stock exists.','UC-38'),
 @('IT-UC35-001','Validate staff account management','Create/edit staff with valid and invalid role, status and email data.','Valid data saves; invalid or duplicate data returns errors without corrupting existing data.','Admin authenticated.','UC-35'),
 @('IT-UC39-001','Aggregate Admin dashboard','Load dashboard with orders, revenue, low-stock and production records.','Dashboard totals match the related database data.','Representative seed data exists.','UC-39'),
 @('IT-UC40-001','Filter Admin order monitoring','Load order list and filter by status, channel and search term.','Only matching orders and correct customer/order details are returned.','Orders in multiple statuses and channels exist.','UC-40'),
 @('IT-RP30-001','Generate revenue report','Request report for a valid date range.','Order counts and paid completed revenue match source data.','Completed orders exist in the selected range.','UC-30'),
 @('IT-RP30-002','Export revenue report','Download CSV for a valid date range and inspect its contents.','CSV is downloadable and contains expected headers and order rows.','Completed and non-completed orders exist.','UC-30'),
 @('IT-UC24-001','Identify low-stock ingredients','Load Admin dashboard with ingredients below, equal to and above threshold.','Ingredients at or below threshold are identified; normal stock is not falsely alerted.','Ingredients at multiple stock levels exist.','UC-24'),
 @('IT-UC24-002','Create low-stock notifications','Trigger low-stock notification creation twice on the same day.','Each Admin receives one notification per low-stock ingredient per day; duplicate notifications are not created.','Active Admins and low-stock ingredients exist.','UC-24'))
$cover=$book.Worksheets.Item('Cover')
SetCell $cover 'B3' 'SWP391-G5 Bakery Management System';SetCell $cover 'F3' 'TinDLB';SetCell $cover 'B4' 'SWP391-G5';SetCell $cover 'F4' (Get-Date -Format 'yyyy-MM-dd');SetCell $cover 'B5' 'SWP391-G5_Integration_Test_v1.0';SetCell $cover 'F5' '1.0';SetCell $cover 'A9' (Get-Date -Format 'yyyy-MM-dd');SetCell $cover 'B9' '1.0';SetCell $cover 'C9' 'A';SetCell $cover 'E9' 'Revised integration test cases for TinDLB functions only';SetCell $cover 'F9' 'UC-24, UC-30, UC-35, UC-37, UC-38, UC-39, UC-40'
$list=$book.Worksheets.Item('Test Cases');SetCell $list 'B3' 'SWP391-G5 Bakery Management System';SetCell $list 'B4' 'SWP391-G5';SetCell $list 'C5' 'ASP.NET Core 8 application; SQL Server/SQLite test database; Microsoft Edge or Google Chrome; Admin, Baker, Staff and Customer test accounts.'
$list.Range('A9:E30').ClearContents();SetCell $list 'A9' 'No';SetCell $list 'B9' 'Function Name';SetCell $list 'C9' 'Sheet Name';SetCell $list 'D9' 'Description';SetCell $list 'E9' 'Pre-Condition'
for($i=0;$i -lt $modules.Count;$i++){ $r=10+$i;SetCell $list "A$r" ($i+1);SetCell $list "B$r" ($modules[$i][0]+' '+$modules[$i][1]);SetCell $list "C$r" $modules[$i][0];SetCell $list "D$r" $modules[$i][2];SetCell $list "E$r" $modules[$i][3] }
$stats=$book.Worksheets.Item('Test Statistics');SetCell $stats 'B3' 'SWP391-G5 Bakery Management System';SetCell $stats 'E3' 'TinDLB';SetCell $stats 'B4' 'SWP391-G5';SetCell $stats 'E4' 'TinDLB';SetCell $stats 'B5' 'SWP391-G5_Integration_Test_v1.0';SetCell $stats 'E5' (Get-Date -Format 'yyyy-MM-dd');SetCell $stats 'B6' 'UC-24, UC-30, UC-35, UC-37, UC-38, UC-39 and UC-40 only.'
$stats.Range('B9:G20').ClearContents();SetCell $stats 'B9' 'No';SetCell $stats 'C9' 'Module code';SetCell $stats 'D9' 'Passed';SetCell $stats 'E9' 'Failed';SetCell $stats 'F9' 'Pending';SetCell $stats 'G9' 'N/A';SetCell $stats 'H9' 'Number of test cases'
for($i=0;$i -lt $modules.Count;$i++){ $r=10+$i;$count=@($cases|Where-Object {$_[5] -eq $modules[$i][0]}).Count;SetCell $stats "B$r" ($i+1);SetCell $stats "C$r" $modules[$i][0];SetCell $stats "D$r" 0;SetCell $stats "E$r" 0;SetCell $stats "F$r" $count;SetCell $stats "G$r" 0;SetCell $stats "H$r" $count }
SetCell $stats 'C18' 'Sub total';SetCell $stats 'D18' 0;SetCell $stats 'E18' 0;SetCell $stats 'F18' $cases.Count;SetCell $stats 'G18' 0;SetCell $stats 'H18' $cases.Count;SetCell $stats 'B20' 'Test coverage';SetCell $stats 'C20' 100;SetCell $stats 'D20' '%';SetCell $stats 'B21' 'Test successful coverage';SetCell $stats 'C21' 0;SetCell $stats 'D21' '%'
$feature=$book.Worksheets.Item('Feature Name1');$feature.Name='TinDLB Functions';$feature=$book.Worksheets.Item('TinDLB Functions');SetCell $feature 'B1' 'TinDLB Functions';SetCell $feature 'B2' 'Integration of Admin controllers, services, database and notification flows for TinDLB use cases.';SetCell $feature 'B3' $cases.Count;SetCell $feature 'B4' 'Round 1';SetCell $feature 'C4' 0;SetCell $feature 'D4' 0;SetCell $feature 'E4' $cases.Count;SetCell $feature 'F4' 0
$feature.Range('A11:P80').ClearContents();$headers=@('Test Case ID','Test Case Description','Test Case Procedure','Expected Results','Pre-conditions','Round 1','Test date','Tester','Round 2','Test date','Tester','Round 3','Test date','Tester','Defect IDs','Note');for($c=0;$c -lt $headers.Count;$c++){SetCell $feature (([char](65+$c))+'11') $headers[$c]};for($i=0;$i -lt $cases.Count;$i++){ $r=12+$i;for($c=0;$c -lt 5;$c++){SetCell $feature (([char](65+$c))+$r) $cases[$i][$c]};SetCell $feature "F$r" 'Pending';SetCell $feature "I$r" 'Pending';SetCell $feature "L$r" 'Pending' }
for($s=$book.Worksheets.Count;$s -ge 1;$s--){$name=$book.Worksheets.Item($s).Name;if($name -eq 'Feature Name2' -or $name -eq 'SecurityTest'){$book.Worksheets.Item($s).Delete()}}
$book.Save();$book.Close();$excel.Quit();[Runtime.InteropServices.Marshal]::ReleaseComObject($excel)|Out-Null
Copy-Item $out 'D:\SWP391-G5-Test\info\Integration-Test-TinDLB.xlsx' -Force
Write-Output "Created $out and D:\SWP391-G5-Test\info\Integration-Test-TinDLB.xlsx"
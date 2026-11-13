Run
Right-click the solution, choose Properties > Startup Project, and select Multiple startup projects.
Set SmartX.Api, SmartX.Simulator and SmartX.Client to Start, and move SmartX.Api to the top of the list.
Press F5. A console window opens for the API, another for the simulator, and the WPF window opens last.

Use the application

Sensor Data Ingestion and Telemetry
 
To register a sensor, enter a MAC address or unique ID, a deployment location and a category, then press **Register Sensor**. To attach a file, select a sensor in the list, press **Attach File...** and pick a configuration file, hardware log or photo. The API stores it under `SmartX.Api/attachments/{sensorId}/`. The Deployment Tree Validator takes one node per line, indented with spaces to nest, and device leaves start with `NODE`. Press **Validate** to run the recursive check. Ticking the simulation box feeds the sparklines, and each sensor shows a control-limit band and a status dot that reacts to the time since its last reading. The sensors created on this page also post telemetry, so a few extra unnamed devices (MAC prefix `24:6F:28:AA`) appear in the Command Stream device list.

Real-Time Command Stream and History
 
Type part of a name, MAC address, location or device type in the search box, and pick a category and an alert state (All, Alerting, Critical, Warning, Offline, Normal) to filter the list. Pressing Enter in the search box logs the search for the pattern engine. Selecting a device charts its last 120 readings with dashed critical limits. For a pump, valve or relay, **Turn ON** and **Turn OFF** issue a manual override, **Shut all valves** sends one bulk command, and **Undo last command** pops the undo stack. Each card under Suggested Actions and Automated Insights shows a reason and a confidence. **Apply** issues the suggested command and **Show device** selects the device, which also carries an amber border in the list while an insight points at it. Learned patterns and active alerts sit below the insights, and the bar at the top shows queue depth, latency, message rate and aggregate load.
 
Network Topology and Mesh Routing
 
The map draws the server, two gateways and 14 nodes with the cost of each link in mW. Green nodes are online, red nodes are down and amber nodes are isolated, and ticking **Highlight minimum spanning tree** draws the MST links in green. To track a route, type a MAC address or part of a name (`24:6F:28:A1:00:0B` or `valve 1`) and press Enter, or click a node. The route to the server appears in amber and in a hop-by-hop list with its cost. For a down or isolated node, the hints list the live neighbours it can hop through. Choosing a gateway under subnet reachability shows which members its subnet reaches, which are down and which are isolated, with the breadth-first visiting order. To simulate a failure, pick a link and press **Cut or restore link**, and the routes and the MST recompute. The historical log shows the tracked node's history. Set **From** and **To** as `yyyy-MM-dd HH:mm` (or `HH:mm` for today) and press **Query**, or use the quick ranges. The status lines report how many tree nodes the query touched.


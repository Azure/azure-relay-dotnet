<p align="center">
  <img src="../relay.png" alt="Microsoft Azure Relay" width="100"/>
</p>

# GetNamespaceInfo.ps1 Instructions

To get a list of the DNS names and IP addresses used for a relay namespace, please follow these steps:

1. Download both GetNamespaceInfo.ps1 and RelayRegionalIPRanges.json into the same folder.
   
2. Run GetNamespaceInfo.ps1 with your target relay namespace (you can also add the endpoint, ex. *namespace*.servicebus.windows.net)
﻿<p align="center">
  <img width="901" height="399" alt="image" src="https://github.com/user-attachments/assets/36f28e1b-cd1b-40ac-9b1c-4c7cf73090ac" />
</p>

3. A 'FUTURE' IPAddress indicates there is no IP associated with the DNS name, but it could potentially be added later.

4. Any new IPs will be allocated from a range based on the namespace region, which is shown at the end:
﻿<p align="center">
<img width="329" height="153" alt="image" src="https://github.com/user-attachments/assets/b256b008-f2a4-45dc-9ca7-f603a2dced6b" />
</p>

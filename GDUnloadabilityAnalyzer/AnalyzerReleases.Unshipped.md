### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
GDU0001 | Unloadability | Warning | [ThreadStatic] field
GDU0002 | Unloadability | Warning | Subscription to external static event
GDU0003 | Unloadability | Warning | GCHandle.Alloc
GDU0004 | Unloadability | Warning | Marshal.GetFunctionPointerForDelegate
GDU0005 | Unloadability | Warning | ThreadPool.RegisterWaitForSingleObject
GDU0006 | Unloadability | Warning | System.Text.Json serialization
GDU0007 | Unloadability | Warning | Newtonsoft.Json serialization
GDU0008 | Unloadability | Warning | XmlSerializer construction
GDU0009 | Unloadability | Warning | TypeDescriptor modification
GDU0010 | Unloadability | Warning | Thread creation
GDU0011 | Unloadability | Warning | Timer creation
GDU0012 | Unloadability | Warning | Encoding.RegisterProvider

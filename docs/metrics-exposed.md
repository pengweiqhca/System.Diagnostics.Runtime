﻿# Metrics exposed

| Category | Name                | Type    | Unit                                                  | Description                                            | Labels | net471 | netcoreapp3.1 | net6.0 |
| -----| ---------------------------------------- | --------- | ---------------------------------------------------------------------------------------------- | --------------- | --------------- | ---------------------------------------- | ---------------------------------------- | ---------------------------------------- |
| assembly   | `assembly.count`    | `Gauge` |      | Number of Assemblies Loaded |        | ✅ | ✅ | ✅ |
| contention | `lock.contention.total` | Counter | | The number of locks contended | |  | ✅ | ✅ |
| contention | `lock.contention.time.total` | Counter | | The total amount of time spent contending locks | |  | ☑️ | ☑️ |
| dns        | `dns.requested.total` | Counter | | The total number of dns lookup requests | |  | | ✅ |
| dns        | `dns.current.count` | Gauge | | The total number of current dns lookups | |  |  | ✅ |
| dns        | `dns.duration.total` | Counter | ms | The sum of dns lookup durations | |  |  | ✅ |
| exception  | `exception.total` | Counter | | Count of exceptions thrown | |  | ☑️ | ☑️ |
| exception  | `exception.total` | Counter |  | Count of exceptions thrown, broken down by type | type |  | ☑️ | ☑️ |
| gc         | `gc.allocated.total` | Counter | B | Allocation bytes since process start | |  | ✅ | ✅ |
| gc         | `gc.fragmentation` | Gauge | % | GC fragmentation | |  | ✅ | ✅ |
| gc         | `gc.memory.total.available` | Gauge | B | The upper limit on the amount of physical memory .NET can allocate to | |  | ✅ | ✅ |
| gc         | `gc.committed.total` | Counter | B | GC Committed bytes since process start | |  |  | ✅ |
| gc         | `gc.collection.time` | Histogram | ms | The amount of time spent running garbage collections | gc_generation gc_type |  | ☑️ | ☑️ |
| gc         | `gc.pause.time` | Histogram | ms | The amount of time execution was paused for garbage collection | |  | ☑️ | ☑️ |
| gc         | `gc.collection.total` | Counter |  | Counts the number of garbage collections that have occurred, broken down by generation number and the reason for the collection. | gc_generation gc_reason |  | ☑️ | ☑️ |
| gc         | `gc.heap.size` | Gauge | B | The current size of all heaps (only updated after a garbage collection) | gc_generation |  | ☑️ | ☑️ |
| gc         | `gc.pinned.objects` | Gauge |  | The number of pinned objects | |  | ☑️ | ☑️ |
| gc         | `gc.finalization.queue.length` | Gauge |  | The number of objects waiting to be finalized | |  | ☑️ | ☑️ |
| gc | `gc.collection.total` | Counter |  | Counts the number of garbage collections that have occurred  | gc_generation | ✅ | ✅ | ✅ |
| gc | `gc.pause.ratio` | Gauge | % | % Time in GC since last GC | |  | 🗸 | 🗸 |
| gc | `gc.heap.size` | Gauge | B | The current size of all heaps (only updated after a garbage collection) | gc_generation |  | 🗸 | 🗸 |
| gc | `gc.heap.size` | Gauge | B | The current size of all heaps | | ✅ | ✅ | ✅ |
| jit        | `jit.il.bytes.total` | Counter | B | IL Bytes Jitted | |  |  | ✅ |
| jit        | `git.method.total` | Counter | | Number of Methods Jitted | |  |  | ✅ |
| jit        | `jit.time.total` | Counter | ms | Time spent in JIT | |  |  | ✅ |
| process | `process.cpu.time` | Counter | s | Processor time of this process | state | ✅ | ✅ | ✅ |
| process    | `process.cpu.count` | Gauge | | The number of available logical CPUs | | ✅ | ✅ | ✅ |
| process    | `process.memory.usage` | Gauge | B | The amount of physical memory in use | | ✅ | ✅ | ✅ |
| process    | `process.memory.virtual` | Gauge | B | The amount of committed virtual memory | | ✅ | ✅ | ✅ |
| process    | `process.cpu.usage` | Gauge | % | CPU usage | | ✅ | ✅ | ✅ |
| process    | `process.handle.count` | Gauge | | Process handle count | | ✅ | ✅ | ✅ |
| process    | `process.thread.count` | Gauge | | Process thread count | | ✅ | ✅ | ✅ |
| sockets    | `sockets.connections.established.outgoing.total` | Counter | | The total number of outgoing established TCP connections | |  |  | ✅ |
| sockets    | `sockets.connections.established.incoming.total` | Counter | B | The total number of incoming established TCP connections | |  |  | ✅ |
| sockets    | `sockets.bytes.received.total` | Counter | B | The total number of bytes received over the network | |  |  | ✅ |
| sockets    | `sockets.bytes.sent.total` | Counter | | The total number of bytes sent over the network | |  |  | ✅ |
| threading  | `threadpool.thread.count` | Gauge | | ThreadPool thread count | |  | ✅ | ✅ |
| threading  | `threadpool.queue.length` | Gauge | | ThreadPool queue length | |  | ✅ | ✅ |
| threading  | `threadpool.completed.items.total` | Counter | | ThreadPool completed work item count | |  | ✅ | ✅ |
| threading  | `threadpool.timer.count` | Gauge | | Number of active timers | |  | ✅ | ✅ |
| threading  | `threadpool.adjustments.total` | Counter | | The total number of changes made to the size of the thread pool, labeled by the reason for change | adjustment_reason |  | ☑️ | ☑️ |
| threading  | `threadpool.io.thread.count` | Gauge | | The number of active threads in the IO thread pool | |  | ☑️ | ☑️ |
| threading | `threadpool.thread.count` | Gauge | | The number of active threads | | ✅ |  |  |
| threading | `threadpool.io.thread.count` | Gauge | | The number of active IO threads | | ✅ |  |  |
| threading | `threadpool.queue.length` | Gauge | | ThreadPool queue length | | ☑️ |  |  |
| threading | `threadpool.completed.items.total` | Counter | | ThreadPool completed work item count | | ☑️ |  |  |


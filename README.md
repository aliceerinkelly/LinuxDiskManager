# LinuxDiskManager

A lightweight, zero-dependency Windows desktop utility written in C# for direct, low-level management and provisioning of UEFI-compliant Linux storage media. 

LinuxDiskManager bypasses typical Windows disk limitations and third-party partition utility paywalls by writing filesystems directly to raw disk sectors (`\\.\PhysicalDriveX`) using standard Win32 APIs and managed sector generation.

---

## Features

* **Zero Third-Party Dependencies:** Does not require Cygwin, MSYS2, `e2fsprogs`, or external `.dll` wrappers. All filesystem structures are generated directly in managed code.
* **Native Managed `ext4` Formatter:** Formats ext4 partitions from scratch via direct sector streaming. Calculates and writes the Superblock (`0xEF53`), Group Descriptor Tables (GDT), block/inode allocation bitmaps, root inode (`/`), and initial directory blocks.
* **Modern Swap Generation:** Synthesizes `SWAPSPACE2` page-0 layouts with custom UUID generation and sector validation.
* **Automated Clean UEFI Disk Prep:**
  * Wipes target disks cleanly to GPT.
  * Creates an EFI System Partition (`ESP`) formatted with FAT32 and hardcodes the UEFI Boot GUID (`C12A7328-F81F-11D2-BA4B-00A0C93EC93B`).
  * Automatically removes the unwanted 16 MB Microsoft Reserved (`MSR`) partition slice created by Windows partitioning tools.
  * Formats Linux Swap and an `ext4` root partition spanning the remaining volume.
* **Interactive Sizing Wizard:** Real-time calculation dialog to customize EFI and Swap partition sizes while auto-computing remaining space for the root partition.
* **Raw Low-Level Sector Inspection:** Inspects live disk sectors directly from Windows to decode and display:
  * FAT32 BPB OEM identifiers, cluster layouts, and volume labels.
  * `ext2/ext3/ext4` Superblock features, block groups, inode counts, and UUIDs.
  * `SWAPSPACE2` headers and page maps.
* **Visual Disk Map & Sequential Mapping:** Displays intuitive partition block visuals and normalizes GPT partition slots into sequential indexes (`1, 2, 3`).

---

## Layout Architecture

The automated preparation sequence configures target drives with three clean, contiguous partitions:

| Partition | Type / GUID | Filesystem | Default Size | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Partition 1** | `C12A7328-F81F-11D2-BA4B-00A0C93EC93B` | FAT32 | 512 MB | UEFI System Partition (`/boot/efi`) |
| **Partition 2** | `0657FD6D-A4AB-43C4-84E5-0933C84B4F4F` | Swap | 8192 MB | Linux Swap Space (`SWAPSPACE2`) |
| **Partition 3** | `0FC63DAF-8483-4772-8E79-3D69D8477DE4` | `ext4` | Remainder | Linux Root Filesystem (`/`) |

---

## Prerequisites & Compilation

* Windows 10 / 11 (64-bit)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download) (or later)
* Administrator privileges (required for raw physical disk handle access via `CreateFile`)

### Building a Single-File Standalone Binary

To build a fully self-contained `.exe` containing the runtime (runs on any modern Windows PC without installing .NET runtimes):

```shell
# Clone the repository
git clone [https://github.com/yourusername/LinuxDiskManager.git](https://github.com/yourusername/LinuxDiskManager.git)
cd LinuxDiskManager

# Publish single-file self-contained binary
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

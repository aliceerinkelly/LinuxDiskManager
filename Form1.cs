using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace LinuxDiskManager;

public enum PartitionCategory
{
    Windows,
    LinuxData,
    LinuxSwap,
    Unknown
}

public class PartitionModel
{
    public int DisplayIndex { get; set; }
    public int PhysicalSlot { get; set; }
    public long StartingOffset { get; set; }
    public long Length { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public PartitionCategory Category { get; set; } = PartitionCategory.Unknown;
    public bool IsBootActive { get; set; }
    public double SizeGb => Length / 1024.0 / 1024.0 / 1024.0;
}

public class DiskModel
{
    public int Index { get; set; }
    public string Name => $@"\\.\PhysicalDrive{Index}";
    public string PartitionStyle { get; set; } = "Unknown";
    public List<PartitionModel> Partitions { get; set; } = new();
    public long TotalAllocatedBytes
    {
        get
        {
            long sum = 0;
            foreach (var p in Partitions) sum += p.Length;
            return sum;
        }
    }
    public double TotalAllocatedGb => TotalAllocatedBytes / 1024.0 / 1024.0 / 1024.0;

    public override string ToString() => $"Disk {Index} ({PartitionStyle}) - {Partitions.Count} Partitions";
}

public class PrepConfigForm : Form
{
    public int EfiSizeMb { get; private set; } = 512;
    public int SwapSizeMb { get; private set; } = 8192;
    public bool UserConfirmed { get; private set; } = false;

    private NumericUpDown numEfi = null!;
    private NumericUpDown numSwap = null!;
    private Label lblLinuxRemaining = null!;
    private readonly double totalDiskGb;

    public PrepConfigForm(int diskIndex, double diskGb)
    {
        this.totalDiskGb = diskGb;
        InitializeLayout(diskIndex);
        UpdateCalculations();
    }

    private void InitializeLayout(int diskIndex)
    {
        this.Text = $"Configure Linux Drive Layout (Disk {diskIndex})";
        this.Size = new Size(580, 440);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(20, 24, 30);
        this.ForeColor = Color.FromArgb(245, 247, 250);
        this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

        Label lblCapacity = new Label
        {
            Text = $"Total Drive Capacity: {totalDiskGb:F2} GB",
            Location = new Point(24, 18),
            AutoSize = true,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White
        };

        Label lblSubtitle = new Label
        {
            Text = "Configure sizes for the three target partitions:",
            Location = new Point(24, 46),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 190, 205)
        };

        // 1. EFI System Partition (Always Bootable)
        Label lblEfi = new Label
        {
            Text = "1. EFI System Partition (FAT32, Boot):",
            Location = new Point(24, 95),
            AutoSize = true
        };
        numEfi = new NumericUpDown
        {
            Location = new Point(310, 91),
            Size = new Size(110, 26),
            Minimum = 100,
            Maximum = 8192,
            Value = 512,
            Increment = 64,
            BackColor = Color.FromArgb(30, 36, 45),
            ForeColor = Color.White
        };
        Label lblEfiUnit = new Label { Text = "MB", Location = new Point(430, 95), AutoSize = true };
        numEfi.ValueChanged += (s, e) => UpdateCalculations();

        // 2. Linux Swap Partition
        Label lblSwap = new Label
        {
            Text = "2. Linux Swap Partition (Swap):",
            Location = new Point(24, 145),
            AutoSize = true
        };
        numSwap = new NumericUpDown
        {
            Location = new Point(310, 141),
            Size = new Size(110, 26),
            Minimum = 512,
            Maximum = 65536,
            Value = 8192,
            Increment = 1024,
            BackColor = Color.FromArgb(30, 36, 45),
            ForeColor = Color.White
        };
        Label lblSwapUnit = new Label { Text = "MB", Location = new Point(430, 145), AutoSize = true };
        numSwap.ValueChanged += (s, e) => UpdateCalculations();

        // 3. Linux ext4 Data Partition
        Label lblLinuxTitle = new Label
        {
            Text = "3. Linux Root / Data (ext4):",
            Location = new Point(24, 195),
            AutoSize = true
        };
        lblLinuxRemaining = new Label
        {
            Text = "Calculating...",
            Location = new Point(310, 195),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.LightGreen
        };

        Label lblNote = new Label
        {
            Text = "The Linux root partition will automatically occupy all remaining drive space.",
            Location = new Point(24, 240),
            Size = new Size(510, 45),
            ForeColor = Color.FromArgb(160, 170, 185),
            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
        };

        Button btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(280, 330),
            Size = new Size(110, 38),
            BackColor = Color.FromArgb(50, 58, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(90, 100, 115);

        Button btnAccept = new Button
        {
            Text = "Apply & Prep",
            Location = new Point(405, 330),
            Size = new Size(135, 38),
            BackColor = Color.FromArgb(34, 110, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAccept.FlatAppearance.BorderColor = Color.FromArgb(60, 145, 110);
        btnAccept.Click += (s, e) =>
        {
            EfiSizeMb = (int)numEfi.Value;
            SwapSizeMb = (int)numSwap.Value;
            UserConfirmed = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        };

        this.Controls.Add(lblCapacity);
        this.Controls.Add(lblSubtitle);
        this.Controls.Add(lblEfi);
        this.Controls.Add(numEfi);
        this.Controls.Add(lblEfiUnit);
        this.Controls.Add(lblSwap);
        this.Controls.Add(numSwap);
        this.Controls.Add(lblSwapUnit);
        this.Controls.Add(lblLinuxTitle);
        this.Controls.Add(lblLinuxRemaining);
        this.Controls.Add(lblNote);
        this.Controls.Add(btnCancel);
        this.Controls.Add(btnAccept);
        this.AcceptButton = btnAccept;
        this.CancelButton = btnCancel;
    }

    private void UpdateCalculations()
    {
        double efiGb = (double)numEfi.Value / 1024.0;
        double swapGb = (double)numSwap.Value / 1024.0;
        double remainingGb = totalDiskGb - efiGb - swapGb;

        if (remainingGb <= 0)
        {
            lblLinuxRemaining.Text = "No Space Remaining!";
            lblLinuxRemaining.ForeColor = Color.Crimson;
        }
        else
        {
            lblLinuxRemaining.Text = $"~{remainingGb:F2} GB (Remaining)";
            lblLinuxRemaining.ForeColor = Color.LightGreen;
        }
    }
}

public partial class Form1 : Form
{
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint IOCTL_DISK_GET_DRIVE_LAYOUT_EX = 0x00070050;

    private static readonly Guid LINUX_FS_DATA_GUID = new("0FC63DAF-8483-4772-8E79-3D69D8477DE4");
    private static readonly Guid LINUX_SWAP_GUID    = new("0657FD6D-A4AB-43C4-84E5-0933C84B4F4F");
    private static readonly Guid LINUX_LVM_GUID     = new("E6D6D379-F507-44C2-A23C-238F2A3DF928");
    private static readonly Guid EFI_SYSTEM_GUID    = new("C12A7328-F81F-11D2-BA4B-00A0C93EC93B");
    private static readonly Guid MSR_PARTITION_GUID = new("E3C9E310-0B56-4C6A-9463-7F755A446A4F");

    private const byte MBR_LINUX_NATIVE = 0x83;
    private const byte MBR_LINUX_SWAP   = 0x82;
    private const byte MBR_LINUX_LVM    = 0x8E;
    private const byte MBR_EFI_SYSTEM   = 0xEF;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFilePointerEx(
        SafeFileHandle hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushFileBuffers(SafeFileHandle hFile);

    // Form Controls
    private Label lblSelectDisk = null!;
    private ComboBox cmbDisks = null!;
    private Button btnRefresh = null!;
    private Panel pnlVisualBar = null!;
    private ListView lstPartitions = null!;
    private Button btnInspect = null!;
    private Button btnInspectEfi = null!;
    private Button btnFormatExt4 = null!;
    private Button btnFormatSwap = null!;
    private Button btnAutoPrep = null!;
    private TextBox txtLog = null!;

    // UI Theme Palette
    private readonly Color BG_DARK = Color.FromArgb(20, 24, 30);
    private readonly Color PANEL_DARK = Color.FromArgb(30, 36, 45);
    private readonly Color TEXT_LIGHT = Color.FromArgb(245, 247, 250);
    private readonly Color TEXT_MUTED = Color.FromArgb(160, 170, 185);
    private readonly Color BUTTON_BG = Color.FromArgb(50, 58, 70);
    private readonly Color BUTTON_HIGHLIGHT = Color.FromArgb(75, 86, 102);
    private readonly Color BUTTON_PREP = Color.FromArgb(34, 110, 80);

    private readonly Color COLOR_LINUX_DATA = Color.FromArgb(46, 139, 87);
    private readonly Color COLOR_LINUX_SWAP = Color.FromArgb(138, 43, 226);
    private readonly Color COLOR_WINDOWS    = Color.FromArgb(30, 144, 255);
    private readonly Color COLOR_UNKNOWN    = Color.FromArgb(80, 85, 95);

    private List<DiskModel> discoveredDisks = new();

    public Form1()
    {
        InitializeComponentLayout();
        Load += async (s, e) => await ScanAllDisksAsync();
    }

    private void InitializeComponentLayout()
    {
        this.Text = "AGC Linux Disk Manager";
        this.Size = new Size(1060, 840);
        this.MinimumSize = new Size(980, 720);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = BG_DARK;
        this.ForeColor = TEXT_LIGHT;
        this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);

        lblSelectDisk = new Label
        {
            Text = "Select Target Disk:",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = TEXT_LIGHT,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
        };

        cmbDisks = new ComboBox
        {
            Location = new Point(190, 17),
            Size = new Size(650, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = PANEL_DARK,
            ForeColor = TEXT_LIGHT,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        cmbDisks.SelectedIndexChanged += CmbDisks_SelectedIndexChanged;

        btnRefresh = CreateStyledButton("Refresh Disks", new Point(855, 15), new Size(160, 32));
        btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefresh.Click += async (s, e) => await ScanAllDisksAsync();

        GroupBox grpVisual = new GroupBox
        {
            Text = "Visual Partition Map",
            Location = new Point(20, 58),
            Size = new Size(995, 120),
            ForeColor = TEXT_LIGHT,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        pnlVisualBar = new Panel
        {
            Location = new Point(15, 25),
            Size = new Size(965, 55),
            BackColor = PANEL_DARK,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlVisualBar.Paint += PnlVisualBar_Paint;

        FlowLayoutPanel pnlLegend = new FlowLayoutPanel
        {
            Location = new Point(15, 88),
            Size = new Size(965, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlLegend.Controls.Add(CreateLegendBadge(COLOR_LINUX_DATA, "Linux ext2/3/4"));
        pnlLegend.Controls.Add(CreateLegendBadge(COLOR_LINUX_SWAP, "Linux Swap"));
        pnlLegend.Controls.Add(CreateLegendBadge(COLOR_WINDOWS, "Windows / EFI"));
        pnlLegend.Controls.Add(CreateLegendBadge(COLOR_UNKNOWN, "Unallocated / Other"));
        grpVisual.Controls.Add(pnlVisualBar);
        grpVisual.Controls.Add(pnlLegend);

        lstPartitions = new ListView
        {
            Location = new Point(20, 190),
            Size = new Size(995, 180),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = PANEL_DARK,
            ForeColor = TEXT_LIGHT,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        lstPartitions.Columns.Add("Part #", 70);
        lstPartitions.Columns.Add("Type / Identity", 290);
        lstPartitions.Columns.Add("Capacity (GB)", 125);
        lstPartitions.Columns.Add("Starting LBA Offset", 220);
        lstPartitions.Columns.Add("Boot / Active", 140);
        lstPartitions.SelectedIndexChanged += (s, e) => UpdateActionButtons();

        // Balanced Action Toolbar (without toggle button)
        btnInspect = CreateStyledButton("Inspect Superblock", new Point(20, 380), new Size(170, 36), true);
        btnInspect.Enabled = false;
        btnInspect.Click += BtnInspect_Click;

        btnInspectEfi = CreateStyledButton("Inspect EFI", new Point(202, 380), new Size(140, 36));
        btnInspectEfi.Enabled = false;
        btnInspectEfi.Click += BtnInspectEfi_Click;

        btnFormatExt4 = CreateStyledButton("Format ext4", new Point(354, 380), new Size(150, 36));
        btnFormatExt4.Enabled = false;
        btnFormatExt4.Click += BtnFormatExt4_Click;

        btnFormatSwap = CreateStyledButton("Format Swap", new Point(516, 380), new Size(150, 36));
        btnFormatSwap.Enabled = false;
        btnFormatSwap.Click += BtnFormatSwap_Click;

        btnAutoPrep = CreateStyledButton("Auto-Prep Linux Drive", new Point(678, 380), new Size(230, 36));
        btnAutoPrep.BackColor = BUTTON_PREP;
        btnAutoPrep.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnAutoPrep.Enabled = false;
        btnAutoPrep.Click += BtnAutoPrep_Click;

        txtLog = new TextBox
        {
            Location = new Point(20, 428),
            Size = new Size(995, 345),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = PANEL_DARK,
            ForeColor = Color.FromArgb(6, 176, 37),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        this.Controls.Add(lblSelectDisk);
        this.Controls.Add(cmbDisks);
        this.Controls.Add(btnRefresh);
        this.Controls.Add(grpVisual);
        this.Controls.Add(lstPartitions);
        this.Controls.Add(btnInspect);
        this.Controls.Add(btnInspectEfi);
        this.Controls.Add(btnFormatExt4);
        this.Controls.Add(btnFormatSwap);
        this.Controls.Add(btnAutoPrep);
        this.Controls.Add(txtLog);
    }

    private Button CreateStyledButton(string text, Point location, Size size, bool highlight = false)
    {
        Button btn = new Button
        {
            Text = text,
            Location = location,
            Size = size,
            BackColor = highlight ? BUTTON_HIGHLIGHT : BUTTON_BG,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(90, 100, 115);
        btn.Font = highlight
            ? new Font("Segoe UI", 9.5F, FontStyle.Bold)
            : new Font("Segoe UI", 9.5F, FontStyle.Regular);

        return btn;
    }

    private Control CreateLegendBadge(Color color, string text)
    {
        Panel p = new Panel { AutoSize = true, Margin = new Padding(0, 0, 20, 0) };
        Panel box = new Panel { Size = new Size(12, 12), BackColor = color, Location = new Point(0, 4) };
        Label lbl = new Label { Text = text, AutoSize = true, Location = new Point(16, 1), ForeColor = TEXT_MUTED };
        p.Controls.Add(box);
        p.Controls.Add(lbl);
        return p;
    }

    private void Log(string msg)
    {
        if (txtLog.InvokeRequired)
        {
            txtLog.Invoke(new Action(() => Log(msg)));
            return;
        }
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
    }

    private void UpdateActionButtons()
    {
        bool hasSelection = lstPartitions.SelectedItems.Count > 0;
        btnInspect.Enabled = hasSelection;
        btnInspectEfi.Enabled = hasSelection;
        btnFormatExt4.Enabled = hasSelection;
        btnFormatSwap.Enabled = hasSelection;
        btnAutoPrep.Enabled = cmbDisks.SelectedItem != null;
    }

    private async Task ScanAllDisksAsync()
    {
        btnRefresh.Enabled = false;
        cmbDisks.Enabled = false;
        btnAutoPrep.Enabled = false;
        discoveredDisks.Clear();
        cmbDisks.Items.Clear();
        lstPartitions.Items.Clear();
        pnlVisualBar.Invalidate();
        UpdateActionButtons();

        Log("Scanning physical storage controllers...");

        await Task.Run(() =>
        {
            for (int driveIndex = 0; driveIndex < 16; driveIndex++)
            {
                string path = $@"\\.\PhysicalDrive{driveIndex}";
                using SafeFileHandle handle = CreateFile(
                    path,
                    GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (handle.IsInvalid) continue;

                DiskModel? disk = ReadDiskLayout(handle, driveIndex);
                if (disk != null)
                {
                    discoveredDisks.Add(disk);
                }
            }
        });

        foreach (var disk in discoveredDisks)
        {
            cmbDisks.Items.Add(disk);
        }

        if (cmbDisks.Items.Count > 0)
        {
            cmbDisks.SelectedIndex = cmbDisks.Items.Count - 1;
        }
        else
        {
            Log("[Warning] No physical disks accessible. Ensure app is run as Administrator.");
        }

        btnRefresh.Enabled = true;
        cmbDisks.Enabled = true;
        btnAutoPrep.Enabled = cmbDisks.SelectedItem != null;
    }

    private DiskModel? ReadDiskLayout(SafeFileHandle handle, int driveIndex)
    {
        uint bufferSize = 16384;
        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

        try
        {
            if (!DeviceIoControl(handle, IOCTL_DISK_GET_DRIVE_LAYOUT_EX, IntPtr.Zero, 0, buffer, bufferSize, out uint bytesReturned, IntPtr.Zero))
                return null;

            int partitionStyle = Marshal.ReadInt32(buffer, 0);
            int partitionCount = Marshal.ReadInt32(buffer, 4);

            DiskModel disk = new DiskModel
            {
                Index = driveIndex,
                PartitionStyle = partitionStyle switch
                {
                    0 => "MBR",
                    1 => "GPT",
                    _ => "RAW"
                }
            };

            int offset = 48;
            int partitionSizeStruct = 144;
            int displayCounter = 1;

            for (int i = 0; i < partitionCount; i++)
            {
                if (offset + partitionSizeStruct > bytesReturned) break;

                int pStyle = Marshal.ReadInt32(buffer, offset);
                long startingOffset = Marshal.ReadInt64(buffer, offset + 8);
                long partitionLength = Marshal.ReadInt64(buffer, offset + 16);
                int partitionNumber = Marshal.ReadInt32(buffer, offset + 24);

                if (partitionLength > 0)
                {
                    PartitionModel part = new PartitionModel
                    {
                        DisplayIndex = displayCounter++,
                        PhysicalSlot = partitionNumber,
                        StartingOffset = startingOffset,
                        Length = partitionLength,
                        Category = PartitionCategory.Windows,
                        TypeName = "Windows / Basic",
                        IsBootActive = false
                    };

                    if (pStyle == 1) // GPT
                    {
                        byte[] guidBytes = new byte[16];
                        Marshal.Copy(buffer + offset + 32, guidBytes, 0, 16);
                        Guid guid = new Guid(guidBytes);

                        if (guid == LINUX_FS_DATA_GUID)
                        {
                            part.Category = PartitionCategory.LinuxData;
                            part.TypeName = "Linux ext2/3/4 (Data)";
                        }
                        else if (guid == LINUX_SWAP_GUID)
                        {
                            part.Category = PartitionCategory.LinuxSwap;
                            part.TypeName = "Linux Swap";
                        }
                        else if (guid == EFI_SYSTEM_GUID)
                        {
                            part.Category = PartitionCategory.Windows;
                            part.TypeName = "EFI System Partition";
                            part.IsBootActive = true; // Hardcoded UEFI ESP Boot target
                        }
                        else if (guid == MSR_PARTITION_GUID)
                        {
                            part.Category = PartitionCategory.Unknown;
                            part.TypeName = "Microsoft Reserved (MSR)";
                        }
                    }
                    else if (pStyle == 0) // MBR
                    {
                        byte type = Marshal.ReadByte(buffer, offset + 32);
                        byte bootIndicator = Marshal.ReadByte(buffer, offset + 33);
                        part.IsBootActive = bootIndicator == 0x80;

                        if (type == MBR_LINUX_NATIVE)
                        {
                            part.Category = PartitionCategory.LinuxData;
                            part.TypeName = "Linux ext2/3/4 (0x83)";
                        }
                        else if (type == MBR_LINUX_SWAP)
                        {
                            part.Category = PartitionCategory.LinuxSwap;
                            part.TypeName = "Linux Swap (0x82)";
                        }
                        else if (type == MBR_EFI_SYSTEM)
                        {
                            part.Category = PartitionCategory.Windows;
                            part.TypeName = "EFI System Partition (0xEF)";
                        }
                    }

                    disk.Partitions.Add(part);
                }

                offset += partitionSizeStruct;
            }

            return disk;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void CmbDisks_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbDisks.SelectedItem is not DiskModel disk) return;

        lstPartitions.Items.Clear();
        UpdateActionButtons();
        Log($"Loaded Disk {disk.Index} layout ({disk.PartitionStyle}). Total: {(disk.TotalAllocatedBytes / 1024.0 / 1024.0 / 1024.0):F2} GB across {disk.Partitions.Count} partitions.");

        int preferredSelectionIndex = -1;

        for (int i = 0; i < disk.Partitions.Count; i++)
        {
            var p = disk.Partitions[i];
            ListViewItem item = new ListViewItem(p.DisplayIndex.ToString()) { Tag = p };
            item.SubItems.Add(p.TypeName);
            item.SubItems.Add($"{p.SizeGb:F2} GB");
            item.SubItems.Add($"{p.StartingOffset:N0}");
            item.SubItems.Add(p.IsBootActive ? "[YES] Boot/ESP" : "No");

            if (p.Category == PartitionCategory.LinuxData)
            {
                item.ForeColor = Color.LightGreen;
                if (preferredSelectionIndex == -1) preferredSelectionIndex = i;
            }
            else if (p.Category == PartitionCategory.LinuxSwap)
            {
                item.ForeColor = Color.MediumPurple;
                if (preferredSelectionIndex == -1) preferredSelectionIndex = i;
            }

            lstPartitions.Items.Add(item);
        }

        if (preferredSelectionIndex >= 0 && lstPartitions.Items.Count > preferredSelectionIndex)
        {
            lstPartitions.Items[preferredSelectionIndex].Selected = true;
            lstPartitions.Focus();
        }

        pnlVisualBar.Invalidate();
    }

    private void PnlVisualBar_Paint(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(PANEL_DARK);

        if (cmbDisks.SelectedItem is not DiskModel disk || disk.Partitions.Count == 0 || disk.TotalAllocatedBytes == 0)
        {
            using Font f = new Font("Segoe UI", 9F, FontStyle.Italic);
            using Brush b = new SolidBrush(TEXT_MUTED);
            g.DrawString("No partitions to visualize for this disk.", f, b, 10, 18);
            return;
        }

        int totalWidth = pnlVisualBar.ClientSize.Width;
        int height = pnlVisualBar.ClientSize.Height;
        float currentX = 0;

        for (int i = 0; i < disk.Partitions.Count; i++)
        {
            var p = disk.Partitions[i];
            float fraction = (float)p.Length / disk.TotalAllocatedBytes;
            float blockWidth = Math.Max(fraction * totalWidth, 6f);

            Color fill = p.Category switch
            {
                PartitionCategory.LinuxData => COLOR_LINUX_DATA,
                PartitionCategory.LinuxSwap => COLOR_LINUX_SWAP,
                PartitionCategory.Windows   => COLOR_WINDOWS,
                _                           => COLOR_UNKNOWN
            };

            RectangleF rect = new RectangleF(currentX, 0, blockWidth, height);
            using (Brush br = new SolidBrush(fill))
            {
                g.FillRectangle(br, rect);
            }

            using (Pen borderPen = new Pen(BG_DARK, 2))
            {
                g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            if (blockWidth > 45)
            {
                string labelPrefix = p.Category == PartitionCategory.LinuxData ? "ext4" :
                                     p.Category == PartitionCategory.LinuxSwap ? "swap" :
                                     p.IsBootActive ? "efi" : "part";

                string text = $"{labelPrefix} #{p.DisplayIndex}";

                using Font blockFont = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                using Brush textBrush = new SolidBrush(Color.White);
                g.DrawString(text, blockFont, textBrush, currentX + 4, height / 2 - 8);
            }

            currentX += blockWidth;
        }
    }

    private void BtnInspect_Click(object? sender, EventArgs e)
    {
        if (cmbDisks.SelectedItem is not DiskModel disk) return;
        if (lstPartitions.SelectedItems.Count == 0 || lstPartitions.SelectedItems[0].Tag is not PartitionModel part) return;

        Log($"Inspecting raw sectors for Disk {disk.Index}, Partition #{part.DisplayIndex}...");

        string devicePath = $@"\\.\PhysicalDrive{disk.Index}";
        using SafeFileHandle handle = CreateFile(
            devicePath,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            Log($"[Error] Unable to obtain read handle to {devicePath}. Win32: {Marshal.GetLastWin32Error()}");
            return;
        }

        if (part.Category == PartitionCategory.LinuxData)
        {
            InspectExtSuperblock(handle, part.StartingOffset);
        }
        else if (part.Category == PartitionCategory.LinuxSwap)
        {
            InspectSwapHeader(handle, part.StartingOffset);
        }
        else
        {
            Log($"Partition #{part.DisplayIndex} is marked as {part.TypeName}. Checking for ext signatures anyway...");
            InspectExtSuperblock(handle, part.StartingOffset);
        }
    }

    private void BtnInspectEfi_Click(object? sender, EventArgs e)
    {
        if (cmbDisks.SelectedItem is not DiskModel disk) return;
        if (lstPartitions.SelectedItems.Count == 0 || lstPartitions.SelectedItems[0].Tag is not PartitionModel part) return;

        Log($"Inspecting EFI / FAT32 Boot Record for Disk {disk.Index}, Partition #{part.DisplayIndex}...");

        string devicePath = $@"\\.\PhysicalDrive{disk.Index}";
        using SafeFileHandle handle = CreateFile(
            devicePath,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            Log($"[Error] Unable to obtain read handle to {devicePath}. Win32: {Marshal.GetLastWin32Error()}");
            return;
        }

        InspectFat32BootSector(handle, part.StartingOffset);
    }

    private void InspectFat32BootSector(SafeFileHandle handle, long partitionOffset)
    {
        if (!SetFilePointerEx(handle, partitionOffset, out _, 0))
        {
            Log($"[Error] Failed to seek to partition start offset {partitionOffset:N0}.");
            return;
        }

        byte[] sector = new byte[512];
        if (!ReadFile(handle, sector, 512, out uint bytesRead, IntPtr.Zero) || bytesRead < 512)
        {
            Log("[Error] Failed reading 512-byte boot sector.");
            return;
        }

        ushort bootSig = BitConverter.ToUInt16(sector, 510);
        if (bootSig != 0xAA55)
        {
            Log($"[Result] Invalid boot signature: 0x{bootSig:X4} (Expected 0xAA55). Not a formatted FAT volume.");
            return;
        }

        string oemName = Encoding.ASCII.GetString(sector, 3, 8).Trim();
        ushort bytesPerSector = BitConverter.ToUInt16(sector, 11);
        byte sectorsPerCluster = sector[13];
        ushort reservedSectors = BitConverter.ToUInt16(sector, 14);
        byte numFats = sector[16];
        uint totalSectors32 = BitConverter.ToUInt32(sector, 32);
        uint sectorsPerFat32 = BitConverter.ToUInt32(sector, 36);
        string fsType = Encoding.ASCII.GetString(sector, 82, 8).Trim();
        string volLabel = Encoding.ASCII.GetString(sector, 71, 11).Trim();

        Log("--------------------------------------------------");
        Log($"[FAT32 / EFI VERIFIED] Format Type: {fsType}");
        Log($" OEM Identifier: {oemName}");
        Log($" Volume Label: {(string.IsNullOrWhiteSpace(volLabel) ? "<NO NAME>" : volLabel)}");
        Log($" Geometry: {bytesPerSector} bytes/sector, {sectorsPerCluster} sectors/cluster");
        Log($" Total Capacity: {(long)totalSectors32 * bytesPerSector / 1024.0 / 1024.0:F2} MB");
        Log("--------------------------------------------------");
    }

    private void InspectExtSuperblock(SafeFileHandle handle, long partitionOffset)
    {
        long targetOffset = partitionOffset + 1024;

        if (!SetFilePointerEx(handle, targetOffset, out _, 0))
        {
            Log($"[Error] Failed to seek to target offset {targetOffset:N0}.");
            return;
        }

        byte[] sb = new byte[1024];
        if (!ReadFile(handle, sb, (uint)sb.Length, out uint bytesRead, IntPtr.Zero) || bytesRead < 1024)
        {
            Log("[Error] Failed reading 1024-byte superblock buffer.");
            return;
        }

        ushort magic = BitConverter.ToUInt16(sb, 56);
        if (magic != 0xEF53)
        {
            Log($"[Result] No valid Ext2/3/4 magic detected. Read: 0x{magic:X4} (Expected 0xEF53).");
            return;
        }

        uint featureIncompat = BitConverter.ToUInt32(sb, 96);
        uint featureRoCompat = BitConverter.ToUInt32(sb, 100);

        string extType = "ext2";
        if ((featureIncompat & 0x0040) != 0 || (featureRoCompat & 0x0008) != 0)
            extType = "ext4 (Extents / Huge File Support)";
        else if ((featureIncompat & 0x0004) != 0)
            extType = "ext3 (Journaled)";

        uint inodesCount = BitConverter.ToUInt32(sb, 0);
        uint blocksCount = BitConverter.ToUInt32(sb, 4);
        uint freeBlocksCount = BitConverter.ToUInt32(sb, 12);
        uint logBlockSize = BitConverter.ToUInt32(sb, 24);
        int blockSize = 1024 << (int)logBlockSize;

        byte[] uuidBytes = new byte[16];
        Buffer.BlockCopy(sb, 104, uuidBytes, 0, 16);
        Guid uuid = new Guid(uuidBytes);

        string label = Encoding.UTF8.GetString(sb, 120, 16).TrimEnd('\0', ' ');
        if (string.IsNullOrWhiteSpace(label)) label = "<no-label>";

        Log("--------------------------------------------------");
        Log($"[SUPERBLOCK VERIFIED] Identified: {extType}");
        Log($" Volume Label: {label}");
        Log($" Filesystem UUID: {uuid}");
        Log($" Block Size: {blockSize:N0} bytes | Total Blocks: {blocksCount:N0}");
        Log($" Free Blocks: {freeBlocksCount:N0} ({(double)freeBlocksCount / blocksCount * 100.0:F1}% free)");
        Log($" Total Inodes: {inodesCount:N0}");
        Log("--------------------------------------------------");
    }

    private void InspectSwapHeader(SafeFileHandle handle, long partitionOffset)
    {
        if (!SetFilePointerEx(handle, partitionOffset, out _, 0))
        {
            Log($"[Error] Failed seeking to partition start {partitionOffset:N0}.");
            return;
        }

        byte[] pageBuffer = new byte[4096];
        if (ReadFile(handle, pageBuffer, 4096, out uint bytesRead, IntPtr.Zero) && bytesRead == 4096)
        {
            string sig = Encoding.ASCII.GetString(pageBuffer, 4086, 10);
            if (sig == "SWAPSPACE2" || sig == "SWAP-SPACE")
            {
                byte[] swapUuid = new byte[16];
                Buffer.BlockCopy(pageBuffer, 1036, swapUuid, 0, 16);

                string ver = sig == "SWAPSPACE2" ? "Version 2 (Modern)" : "Version 1 (Legacy)";
                Log("--------------------------------------------------");
                Log($"[SWAP HEADER VERIFIED] Signature: {sig} ({ver})");
                Log($" Swap UUID: {new Guid(swapUuid)}");
                Log("--------------------------------------------------");
                return;
            }
        }

        Log("[Result] Swap signature not found in the first 4KB page.");
    }

    private async void BtnFormatSwap_Click(object? sender, EventArgs e)
    {
        if (cmbDisks.SelectedItem is not DiskModel disk) return;
        if (lstPartitions.SelectedItems.Count == 0 || lstPartitions.SelectedItems[0].Tag is not PartitionModel part) return;

        DialogResult res = MessageBox.Show(
            $"WARNING: You are about to write a clean Linux Swap header to Disk {disk.Index}, Partition #{part.DisplayIndex} ({part.SizeGb:F2} GB).\n\nContinue?",
            "Confirm Partition Format",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (res != DialogResult.Yes) return;

        SetButtonsEnabled(false);
        Log($"[SWAP FORMAT] Writing Linux Swap header on Disk {disk.Index}, Partition #{part.DisplayIndex}...");

        await Task.Run(() => FormatSwapInternal(disk.Index, part));
        SetButtonsEnabled(true);
    }

    private void FormatSwapInternal(int diskIndex, PartitionModel part)
    {
        string devicePath = $@"\\.\PhysicalDrive{diskIndex}";
        using SafeFileHandle handle = CreateFile(
            devicePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            Log($"[Error] Cannot open physical drive for write. Error: {Marshal.GetLastWin32Error()}");
            return;
        }

        byte[] pageBuffer = new byte[4096];
        Array.Clear(pageBuffer, 0, pageBuffer.Length);

        BitConverter.GetBytes((uint)1).CopyTo(pageBuffer, 1024);
        uint totalPages = (uint)(part.Length / 4096);
        BitConverter.GetBytes(totalPages - 1).CopyTo(pageBuffer, 1028);

        Guid newSwapGuid = Guid.NewGuid();
        newSwapGuid.ToByteArray().CopyTo(pageBuffer, 1036);

        byte[] magic = Encoding.ASCII.GetBytes("SWAPSPACE2");
        Buffer.BlockCopy(magic, 0, pageBuffer, 4086, 10);

        if (!SetFilePointerEx(handle, part.StartingOffset, out _, 0))
        {
            Log($"[Error] Could not seek to offset {part.StartingOffset:N0}.");
            return;
        }

        if (!WriteFile(handle, pageBuffer, (uint)pageBuffer.Length, out uint written, IntPtr.Zero) || written < 4096)
        {
            Log($"[Error] WriteFile failed. Win32 Error: {Marshal.GetLastWin32Error()}");
            return;
        }

        FlushFileBuffers(handle);

        Log("[SWAP FORMAT SUCCESS] Physical sectors committed and flushed.");
        Log($" Assigned UUID: {newSwapGuid}");
        Log($" Total Usable Swap Pages: {totalPages:N0} ({(part.Length / 1024.0 / 1024.0):F2} MB)");
    }

    private async void BtnFormatExt4_Click(object? sender, EventArgs e)
    {
        if (cmbDisks.SelectedItem is not DiskModel disk) return;
        if (lstPartitions.SelectedItems.Count == 0 || lstPartitions.SelectedItems[0].Tag is not PartitionModel part) return;

        DialogResult res = MessageBox.Show(
            $"WARNING: You are about to format Disk {disk.Index}, Partition #{part.DisplayIndex} ({part.SizeGb:F2} GB) as an ext4 filesystem.\n\nContinue?",
            "Confirm ext4 Format",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (res != DialogResult.Yes) return;

        SetButtonsEnabled(false);
        Log($"[EXT4 FORMAT] Initializing native ext4 filesystem on Disk {disk.Index}, Partition #{part.DisplayIndex}...");

        await Task.Run(() => FormatExt4Internal(disk.Index, part, "linux"));
        SetButtonsEnabled(true);
    }

    private void FormatExt4Internal(int diskIndex, PartitionModel part, string volumeLabel)
    {
        string devicePath = $@"\\.\PhysicalDrive{diskIndex}";
        using SafeFileHandle handle = CreateFile(
            devicePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            Log($"[Error] Cannot open physical drive for write. Win32: {Marshal.GetLastWin32Error()}");
            return;
        }

        try
        {
            const uint BLOCK_SIZE = 4096;
            const uint INODE_SIZE = 256;
            const uint BLOCKS_PER_GROUP = 32768;
            const uint INODES_PER_GROUP = 8192;

            uint totalBlocks = (uint)(part.Length / BLOCK_SIZE);
            uint numGroups = (totalBlocks + BLOCKS_PER_GROUP - 1) / BLOCKS_PER_GROUP;
            uint totalInodes = numGroups * INODES_PER_GROUP;

            uint gdtBlocks = (uint)((numGroups * 64 + BLOCK_SIZE - 1) / BLOCK_SIZE);
            uint blockBitmapStart = 1 + gdtBlocks;
            uint inodeBitmapStart = blockBitmapStart + 1;
            uint inodeTableStart  = inodeBitmapStart + 1;
            uint inodeTableBlocks = (INODES_PER_GROUP * INODE_SIZE) / BLOCK_SIZE;
            uint firstDataBlock   = inodeTableStart + inodeTableBlocks;

            byte[] sb = new byte[1024];
            BitConverter.GetBytes(totalInodes).CopyTo(sb, 0);
            BitConverter.GetBytes(totalBlocks).CopyTo(sb, 4);
            BitConverter.GetBytes(totalBlocks - firstDataBlock - 1).CopyTo(sb, 12);
            BitConverter.GetBytes(totalInodes - 11).CopyTo(sb, 16);
            BitConverter.GetBytes((uint)0).CopyTo(sb, 20);
            BitConverter.GetBytes((uint)2).CopyTo(sb, 24);
            BitConverter.GetBytes((uint)2).CopyTo(sb, 28);
            BitConverter.GetBytes(BLOCKS_PER_GROUP).CopyTo(sb, 32);
            BitConverter.GetBytes(BLOCKS_PER_GROUP).CopyTo(sb, 36);
            BitConverter.GetBytes(INODES_PER_GROUP).CopyTo(sb, 40);

            BitConverter.GetBytes((ushort)0xEF53).CopyTo(sb, 56);
            BitConverter.GetBytes((ushort)1).CopyTo(sb, 58);
            BitConverter.GetBytes((ushort)1).CopyTo(sb, 60);
            BitConverter.GetBytes((uint)1).CopyTo(sb, 76);
            BitConverter.GetBytes((ushort)11).CopyTo(sb, 84);
            BitConverter.GetBytes((ushort)INODE_SIZE).CopyTo(sb, 88);

            BitConverter.GetBytes((uint)(0x0002 | 0x0040 | 0x0080)).CopyTo(sb, 96);
            BitConverter.GetBytes((uint)(0x0001 | 0x0002 | 0x0008)).CopyTo(sb, 100);

            Guid fsUuid = Guid.NewGuid();
            fsUuid.ToByteArray().CopyTo(sb, 104);

            byte[] labelBytes = Encoding.UTF8.GetBytes(volumeLabel);
            Array.Copy(labelBytes, 0, sb, 120, Math.Min(labelBytes.Length, 16));
            BitConverter.GetBytes((ushort)64).CopyTo(sb, 254);

            SetFilePointerEx(handle, part.StartingOffset + 1024, out _, 0);
            WriteFile(handle, sb, (uint)sb.Length, out _, IntPtr.Zero);

            byte[] gdt = new byte[gdtBlocks * BLOCK_SIZE];
            for (int g = 0; g < numGroups; g++)
            {
                int gdtOff = g * 64;
                uint grpBlockBitmap = (uint)(g == 0 ? blockBitmapStart : (g * BLOCKS_PER_GROUP));
                uint grpInodeBitmap = (uint)(g == 0 ? inodeBitmapStart : (grpBlockBitmap + 1));
                uint grpInodeTable  = (uint)(g == 0 ? inodeTableStart  : (grpInodeBitmap + 1));

                BitConverter.GetBytes(grpBlockBitmap).CopyTo(gdt, gdtOff + 0);
                BitConverter.GetBytes(grpInodeBitmap).CopyTo(gdt, gdtOff + 4);
                BitConverter.GetBytes(grpInodeTable).CopyTo(gdt, gdtOff + 8);
                BitConverter.GetBytes((ushort)(BLOCKS_PER_GROUP - firstDataBlock)).CopyTo(gdt, gdtOff + 12);
                BitConverter.GetBytes((ushort)(INODES_PER_GROUP - (g == 0 ? 11 : 0))).CopyTo(gdt, gdtOff + 14);
                BitConverter.GetBytes((ushort)(g == 0 ? 1 : 0)).CopyTo(gdt, gdtOff + 16);
                BitConverter.GetBytes((ushort)0x0004).CopyTo(gdt, gdtOff + 18);
            }

            SetFilePointerEx(handle, part.StartingOffset + BLOCK_SIZE, out _, 0);
            WriteFile(handle, gdt, (uint)gdt.Length, out _, IntPtr.Zero);

            byte[] blockBitmap = new byte[BLOCK_SIZE];
            for (uint b = 0; b <= firstDataBlock; b++)
            {
                blockBitmap[b / 8] |= (byte)(1 << (int)(b % 8));
            }
            SetFilePointerEx(handle, part.StartingOffset + (blockBitmapStart * BLOCK_SIZE), out _, 0);
            WriteFile(handle, blockBitmap, BLOCK_SIZE, out _, IntPtr.Zero);

            byte[] inodeBitmap = new byte[BLOCK_SIZE];
            inodeBitmap[0] = 0xFF;
            inodeBitmap[1] = 0x07;
            SetFilePointerEx(handle, part.StartingOffset + (inodeBitmapStart * BLOCK_SIZE), out _, 0);
            WriteFile(handle, inodeBitmap, BLOCK_SIZE, out _, IntPtr.Zero);

            byte[] inodeTable = new byte[BLOCK_SIZE];
            int rootInodeOff = (int)INODE_SIZE;

            BitConverter.GetBytes((ushort)0x41ED).CopyTo(inodeTable, rootInodeOff + 0);
            BitConverter.GetBytes((ushort)0).CopyTo(inodeTable, rootInodeOff + 2);
            BitConverter.GetBytes((uint)BLOCK_SIZE).CopyTo(inodeTable, rootInodeOff + 4);
            BitConverter.GetBytes((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CopyTo(inodeTable, rootInodeOff + 8);
            BitConverter.GetBytes((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CopyTo(inodeTable, rootInodeOff + 12);
            BitConverter.GetBytes((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CopyTo(inodeTable, rootInodeOff + 16);
            BitConverter.GetBytes((ushort)2).CopyTo(inodeTable, rootInodeOff + 26);
            BitConverter.GetBytes((uint)(BLOCK_SIZE / 512)).CopyTo(inodeTable, rootInodeOff + 28);
            BitConverter.GetBytes((uint)0x00080000).CopyTo(inodeTable, rootInodeOff + 32);

            BitConverter.GetBytes((ushort)0xF30A).CopyTo(inodeTable, rootInodeOff + 40);
            BitConverter.GetBytes((ushort)1).CopyTo(inodeTable, rootInodeOff + 42);
            BitConverter.GetBytes((ushort)4).CopyTo(inodeTable, rootInodeOff + 44);
            BitConverter.GetBytes((ushort)0).CopyTo(inodeTable, rootInodeOff + 46);

            BitConverter.GetBytes((uint)0).CopyTo(inodeTable, rootInodeOff + 48);
            BitConverter.GetBytes((ushort)1).CopyTo(inodeTable, rootInodeOff + 52);
            BitConverter.GetBytes((ushort)0).CopyTo(inodeTable, rootInodeOff + 54);
            BitConverter.GetBytes(firstDataBlock).CopyTo(inodeTable, rootInodeOff + 56);

            SetFilePointerEx(handle, part.StartingOffset + (inodeTableStart * BLOCK_SIZE), out _, 0);
            WriteFile(handle, inodeTable, BLOCK_SIZE, out _, IntPtr.Zero);

            byte[] rootDirBlock = new byte[BLOCK_SIZE];
            BitConverter.GetBytes((uint)2).CopyTo(rootDirBlock, 0);
            BitConverter.GetBytes((ushort)12).CopyTo(rootDirBlock, 4);
            rootDirBlock[6] = 1;
            rootDirBlock[7] = 2;
            rootDirBlock[8] = (byte)'.';

            BitConverter.GetBytes((uint)2).CopyTo(rootDirBlock, 12);
            BitConverter.GetBytes((ushort)(BLOCK_SIZE - 12)).CopyTo(rootDirBlock, 16);
            rootDirBlock[18] = 2;
            rootDirBlock[19] = 2;
            rootDirBlock[20] = (byte)'.';
            rootDirBlock[21] = (byte)'.';

            SetFilePointerEx(handle, part.StartingOffset + (firstDataBlock * BLOCK_SIZE), out _, 0);
            WriteFile(handle, rootDirBlock, BLOCK_SIZE, out _, IntPtr.Zero);

            FlushFileBuffers(handle);

            Log("--------------------------------------------------");
            Log("[EXT4 FORMAT SUCCESS] Filesystem written and flushed directly to hardware.");
            Log($" Assigned Volume Label: {volumeLabel}");
            Log($" Assigned Filesystem UUID: {fsUuid}");
            Log($" Total Blocks: {totalBlocks:N0} | Total Block Groups: {numGroups:N0}");
            Log("--------------------------------------------------");
        }
        catch (Exception ex)
        {
            Log($"[Error] Native format exception: {ex.Message}");
        }
    }

    private async void BtnAutoPrep_Click(object? sender, EventArgs e)
    {
        if (cmbDisks.SelectedItem is not DiskModel disk) return;

        using PrepConfigForm cfg = new PrepConfigForm(disk.Index, disk.TotalAllocatedGb > 0 ? disk.TotalAllocatedGb : 111.79);
        if (cfg.ShowDialog(this) != DialogResult.OK || !cfg.UserConfirmed)
        {
            Log("[AUTO-PREP] Operation cancelled by user.");
            return;
        }

        int targetEfiMb = cfg.EfiSizeMb;
        int targetSwapMb = cfg.SwapSizeMb;

        SetButtonsEnabled(false);
        Log($"[AUTO-PREP] Applying custom layout on Disk {disk.Index}: EFI={targetEfiMb}MB, Swap={targetSwapMb}MB, Root=Remaining...");

        await Task.Run(async () =>
        {
            try
            {
                string script = $@"
select disk {disk.Index}
clean
convert gpt
create partition efi size={targetEfiMb}
format quick fs=fat32 label=""EFI""
create partition primary size={targetSwapMb}
set id=0657FD6D-A4AB-43C4-84E5-0933C84B4F4F
create partition primary
set id=0FC63DAF-8483-4772-8E79-3D69D8477DE4
select partition 1
delete partition override
";
                string scriptPath = Path.Combine(Path.GetTempPath(), "autoprep_diskpart.txt");
                File.WriteAllText(scriptPath, script);

                Log("[AUTO-PREP] Partitioning drive via diskpart and removing MSR partition...");

                ProcessStartInfo psi = new()
                {
                    FileName = "diskpart.exe",
                    Arguments = $"/s \"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process proc = new() { StartInfo = psi })
                {
                    proc.Start();
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    try { File.Delete(scriptPath); } catch { }

                    if (proc.ExitCode != 0)
                    {
                        Log($"[Error] diskpart exited with code {proc.ExitCode}:\n{output}");
                        return;
                    }
                }

                Log("[AUTO-PREP] MSR slice stripped; layout committed.");

                await Task.Delay(2000);

                string path = $@"\\.\PhysicalDrive{disk.Index}";
                using SafeFileHandle handle = CreateFile(
                    path,
                    GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    Log($"[Error] Could not open physical drive to write headers. Win32: {Marshal.GetLastWin32Error()}");
                    return;
                }

                DiskModel? updatedDisk = ReadDiskLayout(handle, disk.Index);
                if (updatedDisk == null || updatedDisk.Partitions.Count == 0)
                {
                    Log("[Error] Layout verification failed after partitioning.");
                    return;
                }

                var swapPart = updatedDisk.Partitions.Find(p => p.Category == PartitionCategory.LinuxSwap);
                var dataPart = updatedDisk.Partitions.Find(p => p.Category == PartitionCategory.LinuxData);

                if (swapPart != null)
                {
                    Log($"[AUTO-PREP] Formatting Linux Swap on Partition #{swapPart.DisplayIndex} ({swapPart.SizeGb:F2} GB)...");
                    FormatSwapInternal(disk.Index, swapPart);
                }

                if (dataPart != null)
                {
                    Log($"[AUTO-PREP] Formatting ext4 on Partition #{dataPart.DisplayIndex} ({dataPart.SizeGb:F2} GB)...");
                    FormatExt4Internal(disk.Index, dataPart, "linux");
                }

                Log("==================================================");
                Log("[AUTO-PREP COMPLETE] Drive partitioned and formatted to your custom sizes!");
                Log("==================================================");
            }
            catch (Exception ex)
            {
                Log($"[Error] Auto-prep failed: {ex.Message}");
            }
        });

        await ScanAllDisksAsync();
    }

    private void SetButtonsEnabled(bool enabled)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(() => SetButtonsEnabled(enabled)));
            return;
        }

        bool hasSelection = lstPartitions.SelectedItems.Count > 0;
        btnInspect.Enabled = enabled && hasSelection;
        btnInspectEfi.Enabled = enabled && hasSelection;
        btnFormatExt4.Enabled = enabled && hasSelection;
        btnFormatSwap.Enabled = enabled && hasSelection;
        btnAutoPrep.Enabled = enabled && cmbDisks.SelectedItem != null;
        btnRefresh.Enabled = enabled;
        cmbDisks.Enabled = enabled;
    }
}
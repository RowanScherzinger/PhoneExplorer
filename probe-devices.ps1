$sh = New-Object -ComObject Shell.Application
$pc = $sh.Namespace(17)
foreach ($d in $pc.Items()) {
    "DEVICE: $($d.Name)  |  $($d.Path)  |  folder=$($d.IsFolder) fs=$($d.IsFileSystem)"
    if ($d.IsFileSystem) { continue }
    if (-not $d.IsFolder) { continue }
    foreach ($s in $d.GetFolder.Items()) {
        "  STORAGE: $($s.Name)  |  $($s.Path)"
        $i = 0
        foreach ($c in $s.GetFolder.Items()) {
            "    $($c.Name)  folder=$($c.IsFolder)"
            if ($c.Name -in @('DCIM', 'Pictures')) {
                foreach ($c2 in $c.GetFolder.Items()) {
                    "      $($c2.Name)  folder=$($c2.IsFolder)"
                    if ($c2.IsFolder) {
                        $j = 0
                        foreach ($f in $c2.GetFolder.Items()) { "        $($f.Name)"; if (++$j -ge 4) { break } }
                    }
                }
            }
            if (++$i -ge 40) { "    ..."; break }
        }
    }
}

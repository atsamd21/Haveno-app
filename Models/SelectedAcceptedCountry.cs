﻿namespace Manta.Models;

public class SelectedAcceptedCountry
{
    public string Code { get; set; } = string.Empty;
    public string CodeWithCountry { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

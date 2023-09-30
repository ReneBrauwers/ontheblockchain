using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;


namespace VotingCommission.Models;

public sealed class VotingAnnouncementForm
{

    [Required]
    public string Organisation { get; set; }

    [Required]
    public string Model { get; set; } = "Standard";

    [Required]
    public string Topic { get; set; }

    [Required]
    public List<string> Choices { get; set; }

    [Required]
    public float VotingFeeAmount { get; set; } = 0;

    [Required]
    public DateTime Start { get; set; } = DateTime.Now;

    [Required]
    public DateTime End { get; set; } = DateTime.Now.AddDays(2);

    [Required]
    public bool TrustlineRequired { get; set; } = false;

    public string IssuerAccount { get; set; }

    public string TokenName { get; set; }

}



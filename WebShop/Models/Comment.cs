using System;
using System.Collections.Generic;

#nullable disable

namespace WebShop.Models
{
    public partial class Comment
    {
        public int CommentId { get; set; }
        public string Message { get; set; }
        public DateTime? Created { get; set; }
        public int? CustomerId { get; set; }
        public int? ProductId { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Product Product { get; set; }
    }
}

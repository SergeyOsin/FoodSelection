using System.ComponentModel.DataAnnotations;

namespace UserService.Models
{
    public class CreateDTOUser
    {
        [Required(ErrorMessage ="Имя не должно быть пустым!")]
        [StringLength(50, MinimumLength =2)]
        public string? Name { get; set; } = String.Empty;
    }
}

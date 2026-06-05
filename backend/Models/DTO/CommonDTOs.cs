namespace backend.Models.DTO;

public class PaginatedListDTO
{
    public int PageSize { get; set; }
    public int CurrentPage { get; set; }

    public bool NextPage { get; set; }
}

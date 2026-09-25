namespace GitHubIssueFinder.GitHub;

//The account member GitHub attaches to an item's author and to each assignee. Only the
//login is read; everything else in the object is ignored.
internal sealed class UserDto
{
    public string Login { get; set; }
}

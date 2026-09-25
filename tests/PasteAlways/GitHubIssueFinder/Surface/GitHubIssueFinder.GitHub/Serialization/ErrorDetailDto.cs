namespace GitHubIssueFinder.GitHub;

//One entry of an error body's errors array. Only the human sentence is read.
internal sealed class ErrorDetailDto
{
    public string Message { get; set; }
}

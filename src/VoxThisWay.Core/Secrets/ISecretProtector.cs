namespace VoxThisWay.Core.Secrets;

public interface ISecretProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedValue);
}

using Libro.Desktop.Data;

var password = "Contrase\u00f1a123!";
var stored = args.Length > 0 ? args[0] : "";
if (string.IsNullOrWhiteSpace(stored))
{
    var generated = Scrypt.HashPassword(password);
    Console.WriteLine(generated);
    Console.WriteLine(Scrypt.VerifyPassword(password, generated) ? "generated_ok" : "generated_fail");
    Console.WriteLine(Scrypt.VerifyPassword(password + "x", generated) ? "bad_accepted" : "bad_rejected");
    return;
}

Console.WriteLine(Scrypt.VerifyPassword(password, stored) ? "vector_ok" : "vector_fail");

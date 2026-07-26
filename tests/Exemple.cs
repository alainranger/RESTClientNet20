using System;
using ApiUtils;

namespace ApiUtils.Exemple
{
    // Exemple de modèle correspondant au JSON retourné par l'API
    public class Utilisateur
    {
        private int _id;
        private string _nom;

        public int Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public string Nom
        {
            get { return _nom; }
            set { _nom = value; }
        }
    }

    public class Programme
    {
        public static void Main()
        {
            ApiClient client = new ApiClient("https://mon-api.example.com/");
            client.AddDefaultHeader("Authorization", "Bearer MON_TOKEN");

            // Timeout par tentative : 5 secondes
            client.Timeout = 5000;

            // Timeout global couvrant toutes les tentatives : 20 secondes
            client.OperationTimeout = 20000;

            // Retry : 4 tentatives, délai de base 500ms, backoff exponentiel (500ms, 1s, 2s, 4s...)
            client.RetryPolicy = new RetryPolicy(4, 500, true);

            // Circuit breaker : s'ouvre après 5 échecs consécutifs, reste ouvert 30 secondes
            // Important : réutiliser la même instance de CircuitBreaker entre plusieurs appels
            // (ou plusieurs ApiClient visant la même API) pour que le compteur soit cumulatif.
            CircuitBreaker circuitBreaker = new CircuitBreaker(5, TimeSpan.FromSeconds(30));
            client.CircuitBreaker = circuitBreaker;

            // GET
            ApiResponse<Utilisateur> reponse = client.Get<Utilisateur>("api/utilisateurs/1");

            if (reponse.Success)
            {
                Console.WriteLine("Utilisateur : " + reponse.Data.Nom);
            }
            else if (circuitBreaker.State == CircuitBreakerState.Open)
            {
                Console.WriteLine("Service indisponible (circuit ouvert) : " + reponse.ErrorMessage);
            }
            else
            {
                Console.WriteLine("Erreur (" + reponse.StatusCode + ") : " + reponse.ErrorMessage);
            }

            // POST
            Utilisateur nouvel_utilisateur = new Utilisateur();
            nouvel_utilisateur.Nom = "Alain";

            ApiResponse<Utilisateur> reponseCreation = client.Post<Utilisateur>("api/utilisateurs", nouvel_utilisateur);

            if (reponseCreation.Success)
            {
                Console.WriteLine("Créé avec l'id : " + reponseCreation.Data.Id);
            }
        }
    }
}

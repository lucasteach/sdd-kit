# Doctrine SDD-Kit v1.1

1. **Spec avant code** : aucun code fonctionnel sans spec approuvée.
2. **Brouillon → ratification → phases bornées** : périmètre fermé,
   jalon visible.
3. **Décisions datées et statutées** : ouverte / ratifiée / différée ;
   une décision différée nomme sa phase de résolution.
4. **Commits atomiques**, tree propre, message type(scope): sujet,
   hash cité dans les reports.
5. **Une tâche bornée à la fois** par agent ; jamais deux agents sur
   le même working tree.
6. **Anti-oubli** : toute idée reportée s'enregistre au backlog
   dans le commit qui la pose.
7. **Mémoire du repo** : AGENT_STATE à chaque phase ou 3 commits ;
   nouveau chat = « Lis docs/AGENT_STATE.md et continue ».
8. **Validation du responsable** : aucune tâche visuelle close sans capture ou
   vérification du responsable consignée.
9. **Couche d'honnêteté** : données → rendu réel ; absence → état
   vide avec action ; échec → alerte lisible ; succès → seulement si
   N > 0.
10. **Frontière outillage** : les outils internes ne fuient pas dans
    le code produit ; seules les bibliothèques publiques voyagent.
11. **Séparation des namespaces** : `Dn` nomme une décision de spec,
    `#N` nomme une règle dure de doctrine. Jamais l'inverse.
    - *Exemple* : « D3 ratifiée » renvoie à la spec ; « règle #12 » renvoie
      à cette doctrine.
    - *Anti-pattern* : écrire « règle 3 » pour une décision de spec, ou
      « D12 » pour une règle de doctrine — le lecteur ne sait plus quoi relire.
12. **Audit forensique avant affirmation** : une hypothèse n'est pas une
    source tant qu'elle n'a pas été vérifiée contre le disque (fichier,
    ligne, sortie de commande).
    - *Exemple* : « le champ est absent » s'écrit après un `grep -n`, pas
      après un souvenir de lecture.
    - *Anti-pattern* : affirmer qu'un fichier contient X parce qu'un rapport
      antérieur l'affirmait.
13. **Mémoire d'agent = cache, jamais source** : ce qui n'est pas dans le
    repo n'existe pas ; une conversation, un résumé ou un contexte de chat
    est jeté dès qu'il contredit le repo.
    - *Exemple* : un hash de commit se relit dans `git log`, jamais de mémoire.
    - *Anti-pattern* : « d'après la session précédente, le test passe ».
14. **Mémoire volatile** : `AGENT_STATE.md` reste sous 5 Ko et ne porte que
    l'état courant ; le détail forensique part dans `HISTORY_ARCHIVE`.
    - *Exemple* : table des commits de session bornée aux dernières entrées,
      le reste archivé.
    - *Anti-pattern* : un AGENT_STATE qui rejoue tout l'historique jusqu'à
      devenir illisible pour l'agent suivant.

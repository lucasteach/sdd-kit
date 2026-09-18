# Doctrine SDD-Kit v1.0

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

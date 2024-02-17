

/**
 * @param ctx {AdvertisingSetup}
 * @param kind {number}
 */
async function hire(ctx, kind) {
    const allow_remote = await ctx.askChoice("Télétravail", ["En distanciel", "Au choix", "Sur place"]);
    let where = null;
    if (allow_remote === 1 || allow_remote === 2)
        where = await ctx.askUser("Où se situent les bureaux ? (ville / pays)");

    const duration = await ctx.askUser("Quelle est la durée du contrat ?");
    const responsibility = await ctx.askUser("Quelles seront les responsabilités et le travail à réaliser ?");
    const qualification = await ctx.askUser("Quelles sont les qualifications recherchées ?");
    let counterpart = null;
    if (kind === AdvertisingSetup.KIND_FREELANCE.type)
        counterpart = await ctx.askUser("Personne ne veut travailler gratuitement. Précises ce que gagne la personne à travailler avec toi !");
    const how = await ctx.askUser("Comment peut-on postuler ? discord, mail (préciser le mail)...");
    const links = await ctx.askUser("Tu peux ajouter quelques liens utils si tu le souhaites (portfolio, site web etc..)", false);
}


module.exports = {hire_freelance};
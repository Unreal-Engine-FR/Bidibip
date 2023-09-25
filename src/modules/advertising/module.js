// MODULE ADVERTISING
const {CommandInfo} = require("../../utils/interactionBase")
const {Message} = require('../../utils/message')
const {Embed} = require('../../utils/embed')
const {Channel} = require("../../utils/channel");
const {ModuleBase} = require("../../utils/module_base");

const PENDING_REQUESTS = {}

class Module extends ModuleBase {
    constructor(create_infos) {
        super(create_infos)
        this.client = create_infos.client

        this.commands = [
            new CommandInfo('paid', 'Ajouter une annonce payante', this.paid)
                .add_text_option('remuneration', 'Comment le travail sera t-il compensé ?', ['Rémunération', 'Si le jeu fonctionne, partage des revenus', 'Pas de rémunération'])
                .add_text_option('contrat', 'Est-ce un contrat permanent ou contractuel ?', ['Permanent', 'Contractuel'])
                .add_text_option('role', 'Quel rôle recrutes-tu ? (Gameplay developer...)')
                .add_text_option('societe', 'Quel est le nom de l\'entreprise ?')
                .add_text_option('remote', 'Est-ce que le remote est possible ?', ['🌐 Distanciel accepté', '🏣 Presentiel seulement'])
                .add_text_option('responsabilites', 'Liste des responsabilites associes pour ce rôle ?')
                .add_text_option('qualifications', 'Lister les qualifications pour ce rôle.')
                .add_text_option('postuler', 'Comment peut-on postuler ?')
                .add_text_option('localisation', 'Ou est localisé l\'entreprise ?', [], false)
                .add_text_option('duree', 'Durée dans le cas d\'un contrat non Permanent', [], false)
                .set_member_only(),
            new CommandInfo('freelance', 'Ajouter une annonce de freelance', this.freelance)
                .add_text_option('nom', 'Quel est ton nom, ou le nom de ton studio ?')
                .add_text_option('role', 'Quel sera le rôle de la personne recrutée dans le studio ?')
                .add_text_option('portfolio', 'Entrez l\'url de votre site portfolio (URL requis)')
                .add_text_option('services', 'Quel est la liste des services que tu proposes ?')
                .add_text_option('contact', 'Comment les clients potentiels peuvent-ils vous contacter ?')
                .set_member_only(),
            new CommandInfo('unpaid', 'Ajouter une annonce bénévole', this.unpaid)
                .add_text_option('titre', 'Ajoute un titre qui définit clairement ce que tu cherches')
                .add_text_option('description', 'Ajoute une description détaillée du projet et ce dont tu as besoin')
                .add_text_option('contact', 'Comment peut-on te contacter ?')
                .set_member_only(),
        ]
    }

    /**
     * @param command_interaction {CommandInteraction}
     * @return {Promise<void>}
     */
    async paid(command_interaction) {
        const result = this.build_paid(command_interaction)

        if (!result.valid) {
            await command_interaction.reply(result.message)
            return
        }

        if (await this.ask_user_confirmation(command_interaction, result.message
            .set_text('Prends le temps de vérifier ton message :')) === true) {

            await this.build_paid(command_interaction).message.set_channel(new Channel().set_id(this.app_config.ADVERTISING_PAID_CHANNEL))
                .set_text(`Nouvelle annonce de ${command_interaction.author().mention()} !`).send()
            await this.build_paid(command_interaction).message.set_channel(new Channel().set_id(this.app_config.SHARED_SHARED_CHANNEL))
                .set_text(`Nouvelle annonce de ${command_interaction.author().mention()} !`).send()
                .then(message => {
                    command_interaction.edit_reply(new Message()
                        .set_text(`Ton annonce a bien été publiée : ${message.url()}`))
                        .catch(err => console.fatal(`failed to edit reply : ${err}`))
                })
        } else {
            await command_interaction.delete_reply()
                .catch(err => console.fatal(`Failed to delete reply ${err}`))
        }
    }

    /**
     * @param command_interaction {CommandInteraction}
     * @return {Promise<void>}
     */
    async unpaid(command_interaction) {
        const result = this.build_unpaid(command_interaction)

        if (!result.valid) {
            await command_interaction.reply(result.message)
            return
        }
        if (await this.ask_user_confirmation(command_interaction, result.message
            .set_text('Prends le temps de vérifier ton message :')) === true) {

            await this.build_unpaid(command_interaction).message.set_channel(new Channel().set_id(this.app_config.ADVERTISING_UNPAID_CHANNEL))
                .set_text(`Nouvelle annonce de ${command_interaction.author().mention()} !`).send()
            await this.build_unpaid(command_interaction).message.set_channel(new Channel().set_id(this.app_config.SHARED_SHARED_CHANNEL))
                .set_text(`Nouvelle annonce de ${command_interaction.author().mention()} !`).send()
                .then(message => {
                    command_interaction.edit_reply(new Message()
                        .set_text(`Ton annonce a bien été publiée : ${message.url()}`))
                        .catch(err => console.fatal(`failed to edit reply : ${err}`))
                })
        } else {
            await command_interaction.delete_reply()
                .catch(err => console.fatal(`Failed to delete reply ${err}`))
        }
    }

    /**
     * @param command_interaction {CommandInteraction}
     * @return {Promise<void>}
     */
    async freelance(command_interaction) {
        const result = this.build_freelance(command_interaction)

        if (!result.valid) {
            await command_interaction.reply(result.message)
            return
        }

        if (await this.ask_user_confirmation(command_interaction, result.message
            .set_text('Prends le temps de vérifier ton message :')) === true) {

            await this.build_freelance(command_interaction).message.set_channel(new Channel().set_id(this.app_config.ADVERTISING_FREELANCE_CHANNEL))
                .set_text(`Nouvelle annonce de ${command_interaction.author().mention()} !`).send()
            await this.build_freelance(command_interaction).message.set_channel(new Channel().set_id(this.app_config.SHARED_SHARED_CHANNEL))
                .set_text(`Nouvelle annonce de ${command_interaction.author().mention()} !`).send()
                .then(message => {
                    command_interaction.edit_reply(new Message()
                        .set_text(`Ton annonce a bien été publiée : ${message.url()}`))
                        .catch(err => console.fatal(`failed to edit reply : ${err}`))
                })
        } else {
            await command_interaction.delete_reply()
                .catch(err => console.fatal(`Failed to delete reply ${err}`))
        }
    }

    build_paid(command) {
        if (command.read('remuneration') !== 'Rémunération')
            return {
                message: new Message()
                    .set_text('Pour les projets de loisirs ou pour tout autre type de payement, veuillez utiliser la commande /unpaid.')
                    .set_client_only(),
                valid: false
            }

        if (command.read('contrat') === 'Contractuel' && !command.read('duree'))
            return {
                message: new Message()
                    .set_text('Veuillez spécifier l\'option \'duree\' dans le cas d\'un contrat temporaire')
                    .set_client_only(),
                valid: false
            }

        const duree = command.read('contrat') === 'Contractuel' ? command.read('duree') : 'permanent'

        const embed = new Embed()
            .set_author(command.author())
            .set_color('#65eb34')
            .set_title((command.read('role') || 'option manquante') + " Chez " + (command.read('societe') || 'option manquante'))
            .set_description(command.read('remote') || 'option manquante')
            .add_field('Durée du contrat', duree, true)

        if (command.read('localisation'))
            embed.add_field('Localisation', command.read('localisation'), true)

        embed.add_field('Responsabilités', command.read('responsabilites') || 'valeur manquante')
            .add_field('Qualifications\n', command.read('qualifications') || 'valeur manquante')
            .add_field('Comment postuler\n', command.read('postuler') || 'valeur manquante')

        return {
            message: new Message()
                .set_channel(command.channel())
                .add_embed(embed),
            valid: true
        }
    }

    build_unpaid(command) {
        return {
            message: new Message()
                .set_channel(command.channel())
                .add_embed(new Embed()
                    .set_color('#65eb34')
                    .set_author(command.author())
                    .set_title(command.read('titre') || 'Option manquante')
                    .set_description(command.read('description') || 'Option manquante')
                    .add_field('contact', command.read('contact') || 'Option manquante')
                ),
            valid: true
        }
    }

    build_freelance(command) {
        const url = command.read('portfolio') || 'option manquante'
        const url_regex = /https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\\+.~#?&/=]*)/g
        if (!url_regex.test(url)) {
            return {message: new Message().set_client_only().set_text('Le portfolio doit être une URL'), valid: false}
        }

        return {
            message: new Message()
                .set_channel(command.channel())
                .add_embed(new Embed()
                    .set_color('#65eb34')
                    .set_embed_author_name(command.read('nom') || 'Option manquante')
                    .set_title(`${command.read('role')} chez ${command.read('nom') || 'Option manquante'}`)
                    .set_author(command.author())
                    .set_description(url)
                    .add_field('Services', command.read('services') || 'Option manquante')
                    .add_field('Contacts', command.read('contact') || 'Option manquante')),
            valid: true
        }
    }
}

module.exports = {Module}
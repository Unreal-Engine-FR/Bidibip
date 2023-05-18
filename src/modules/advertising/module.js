// MODULE ADVERTISING
const {CommandInfo, Message, Embed, Button} = require("../../discord_interface");
const CONFIG = require('../../config').get()
MODULE_MANAGER = require("../../module_manager").get()

class Module {
    constructor(create_infos) {
        this.client = create_infos.client

        this.pending_request = {}

        this.commands = [
            new CommandInfo('paid', 'Ajouter une annonce payante')
                .add_text_option('remuneration', 'Comment le travail sera t-il compensé ?', ['Rémunération', 'Si le jeu fonctionne, partage des revenus', 'Pas de rémunération'])
                .add_text_option('contrat', 'Est-ce un contrat permanent ou contractuel ?', ['Permanent', 'Contractuel'])
                .add_text_option('role', 'Quel rôle recrutes-tu ? (Gameplay developer...)')
                .add_text_option('societe', 'Quel est le nom de l\'entreprise ?')
                .add_bool_option('remote', 'Est-ce que le remote est possible ?')
                .add_text_option('responsabilites', 'Liste des responsabilites associes pour ce rôle ?')
                .add_text_option('qualifications', 'Lister les qualifications pour ce rôle.')
                .add_text_option('postuler', 'Comment peut-on postuler ?')
                .add_text_option('localisation', 'Ou est localisé l\'entreprise ?', [], false)
                .add_text_option('duree', 'Durée dans le cas d\'un contrat non Permanent', [], false)
                .set_member_only(),
            new CommandInfo('freelance', 'Ajouter une annonce de freelance')
                .add_text_option('nom', 'Quel est ton nom, ou le nom de ton studio ?')
                .add_text_option('portfolio', 'Entrez l\'url de votre site portfolio (URL requis)')
                .add_text_option('services', 'Quel est la liste des services que tu proposes ?')
                .add_text_option('contact', 'Comment les clients potentiels peuvent-ils vous contacter ?')
                .set_member_only(),
            new CommandInfo('unpaid', 'Ajouter une annonce bénévole')
                .add_text_option('titre', 'Ajoute un titre qui définit clairement ce que tu cherches')
                .add_text_option('description', 'Ajoute une description détaillée du projet et ce dont tu as besoin')
                .add_text_option('contact', 'Comment peut-on te contacter ?')
                .set_member_only(),
        ]
    }

    // When server command is executed
    server_command(command) {

        if (command.match('paid')) {
            const result = this._build_paid(command)

            if (!result.valid) {
                command.reply(result.message)
                return
            }

            command.reply(result.message
                .set_client_only()
                .set_text('Prends le temps de vérifier ton message :')
                .add_interaction_row([
                    new Button('cancel', 'Annuler').set_danger(),
                    new Button('send', 'Envoyer').set_success()
                ])).then(id => {
                this.pending_request[id] = {command: command, message: this._build_paid(command).message}
                MODULE_MANAGER.event_manager().watch_interaction(this, id)
            })
        }
        if (command.match('unpaid')) {
            command.reply(this._build_unpaid(command)
                .set_client_only()
                .set_text('Prends le temps de vérifier ton message :')
                .add_interaction_row([
                    new Button('cancel', 'Annuler').set_danger(),
                    new Button('send', 'Envoyer').set_success()
                ])).then(id => {
                this.pending_request[id] = {command: command, message: this._build_unpaid(command)}
                MODULE_MANAGER.event_manager().watch_interaction(this, id)
            })
        }
        if (command.match('freelance')) {
            const result = this._build_freelance(command)

            if (!result.valid) {
                command.reply(result.message)
                return
            }

            command.reply(result.message
                .set_client_only()
                .set_text('Prends le temps de vérifier ton message :')
                .add_interaction_row([
                    new Button('cancel', 'Annuler').set_danger(),
                    new Button('send', 'Envoyer').set_success()
                ])).then(id => {
                this.pending_request[id] = {command: command, message: this._build_freelance(command).message}
                MODULE_MANAGER.event_manager().watch_interaction(this, id)
            })
        }
    }

    receive_interaction(value, id, message) {
        MODULE_MANAGER.event_manager().release_interaction(this, id)
        const output_message = this.pending_request[id]
        if (value === 'send') {
            this.client.say(output_message.message.set_channel(CONFIG.ADVERT_UNPAID_ID))
            this.client.say(output_message.message.set_channel(CONFIG.SHARED))
                .then(message => {
                    output_message.command.edit_reply(new Message()
                        .set_text(`Ton annonce a bien été publiée : https://discord.com/channels/${CONFIG.SERVER_ID}/${message.channel}/${message.source_id}`))
                })
        } else
            output_message.command.delete_reply()
    }

    _build_paid(command) {

        if (command.option_value('remuneration') !== 'Rémunération')
            return {
                message: new Message()
                    .set_text('Pour les projets de loisirs ou pour tout autre type de payement, veuillez utiliser la commande /unpaid.')
                    .set_client_only(),
                valid: false
            }

        if (command.option_value('contrat') === 'Contractuel' && !command.option_value('duree'))
            return {
                message: new Message()
                    .set_text('Veuillez spécifier l\'option \'duree\' dans le cas d\'un contrat temporaire')
                    .set_client_only(),
                valid: false
            }

        const duree = command.option_value('contrat') === 'Contractuel' ? command.option_value('duree') : 'permanent'

        const embed = new Embed()
            .set_title((command.option_value('role') || 'option manquante') + " Chez " + (command.option_value('societe') || 'option manquante'))
            .set_description(command.option_value('remote') === true ? ':globe_with_meridians: Remote accepté' : ':post_office: Présentiel')
            .add_field('Durée du contrat', duree, true)

        if (command.option_value('localisation'))
            embed.add_field('Localisation', command.option_value('localisation'), true)

        embed.add_field('Responsabilités', command.option_value('responsabilites') || 'valeur manquante')
            .add_field('Qualifications\n', command.option_value('qualifications') || 'valeur manquante')
            .add_field('Comment postuler\n', command.option_value('postuler') || 'valeur manquante')

        return {
            message: new Message()
                .add_embed(embed),
            valid: true
        }
    }

    _build_unpaid(command) {
        return new Message()
            .add_embed(new Embed()
                .set_title(command.option_value('titre') || 'Option manquante')
                .set_description(command.option_value('description') || 'Option manquante')
                .add_field('contact', command.option_value('contact') || 'Option manquante')
            )
    }

    _build_freelance(command) {

        const url = command.option_value('portfolio') || 'option manquante'
        const url_regex = /https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\\+.~#?&//=]*)/g;
        if (!url_regex.test(url)) {
            return {message: new Message().set_client_only().set_text('Le portfolio doit être une URL'), valid: false}
        }

        return {
            message: new Message()
                .add_embed(new Embed()
                    .set_title(command.option_value('nom') || 'Option manquante')
                    .set_description(url)
                    .add_field('Services', command.option_value('services') || 'Option manquante')
                    .add_field('Contacts', command.option_value('contact') || 'Option manquante')),
            valid: true
        }
    }
}

module.exports = {Module}
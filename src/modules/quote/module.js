// MODULE QUOTE
const {CommandInfo, Message, Embed} = require("../../discord_interface");

const fs = require('fs')

class Module {
    constructor(create_infos) {
        this.client = create_infos.client

        this.commands = [
            new CommandInfo('quote', 'Envoyer une citation de la personne choisie')
                .add_user_option('utilisateur', 'personne choisie'),
            new CommandInfo('add_quote', 'Ajoute une citation à la base de donnée')
                .add_text_option('message', 'id du message à citer')
        ]

        try {
            const data = fs.readFileSync(__dirname + '/data/quotes.json', 'utf8');
            this.quotes = JSON.parse(data)
        } catch (err) {
            this.quotes = {}
        }
    }

    server_command(command) {
        if (command.match('quote')) {
            const quote = this.quotes[command.option_value('utilisateur')]
            if (!quote) {
                command.reply(new Message().set_text(`Je n'ai pas de citation pour cet utilisateur... Mais je suis sûr qu'il est très cool !`).set_client_only())
                return;
            }

            const selected_quote = quote[Math.floor(Math.random() * quote.length)];

            this.client.say(
                new Message()
                    .set_channel(command.channel)
                    .add_embed(new Embed()
                        .set_title(selected_quote)
                        .set_description(`*${this.client.get_user_name(command.option_value('utilisateur'))}* - Demandé par ${this.client.get_user_name(command.owner, true)}`))
            )
            command.skip()
        }

        if (command.match('add_quote')) {
            this.client.get_message(command.channel, command.option_value('message'))
                .then(message => {
                    if (!this.quotes[message.author.id])
                        this.quotes[message.author.id] = [message.text]
                    else
                        this.quotes[message.author.id].push(message)

                    fs.writeFile(__dirname + '/data/quotes.json', JSON.stringify(this.quotes), 'utf8', err => console.log(`failed to save quotes : ${err}`))

                    command.reply(new Message().set_text('Citation ajoutée à la base de donnée !'))
                })
                .catch(err => {
                    console.log(`failed to get message : ${err}`)
                    command.reply(new Message().set_text(`Je n'ai pas trouvé le message :(`).set_client_only())
                })
        }
    }
}

module.exports = {Module}
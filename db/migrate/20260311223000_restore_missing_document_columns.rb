class RestoreMissingDocumentColumns < ActiveRecord::Migration[8.1]
  def change
    unless column_exists?(:documents, :metadata_ip)
      add_column :documents, :metadata_ip, :string
    end

    unless column_exists?(:documents, :metadata_protocol)
      add_column :documents, :metadata_protocol, :string
    end

    unless column_exists?(:documents, :metadata_filename)
      add_column :documents, :metadata_filename, :string
    end

    unless column_exists?(:documents, :records)
      add_column :documents, :records, :json, default: [], null: false
    end

    unless column_exists?(:documents, :new_untitled_placeholder)
      add_column :documents, :new_untitled_placeholder, :boolean, default: false, null: false
    end
  end
end